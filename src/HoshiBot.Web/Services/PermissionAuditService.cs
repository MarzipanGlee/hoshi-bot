using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NetCord;
using NetCord.Rest;

namespace HoshiBot.Web.Services;

// The Permission Check page's two audit engines (expectation audit + bot posting access) and
// their fix paths, extracted from PermissionCheck.razor — components stay UI/state glue, the
// Discord REST/EF work and the pure permission math live here. The page (and its section
// child components) bind the result records this service computes.
//
// User-visible strings come from the message catalog with the Language passed EXPLICITLY by
// the calling component (never resolved/injected here): the results live in per-circuit
// component state, and nothing localized may end up in the shared IMemoryCache (its entries
// cross circuits/users — see the localization plan). This service only caches raw Discord
// data, never the strings it renders from it.
public sealed class PermissionAuditService(
    IDbContextFactory<HoshiBotDbContext> dbFactory,
    DiscordGuildDataService discordData,
    BotChannelRequirementService requirementService,
    RestClient botRestClient,
    IMemoryCache cache,
    IConfiguration configuration)
{
    public static readonly Permissions[] AllPermissions = Enum.GetValues<Permissions>();

    private static bool IsManage(Permissions perms) =>
        perms.HasFlag(Permissions.Administrator) || perms.HasFlag(Permissions.ManageRoles);

    private static string Mark(bool granted) => granted ? "✅" : "❌";

    // Display label per permission — catalog-backed for the handful the audits require;
    // any other flag falls back to its enum name (Msg.WebAudit.Perm's own fallback).
    public static string PermLabel(Language lang, Permissions permission) =>
        Msg.WebAudit.Perm(lang, permission.ToString());

    // Each individual required permission with its granted state, e.g. "✅ View Channel · ❌ Send Messages".
    public static string PermsSummary(Language lang, Permissions required, Permissions effective) =>
        string.Join(" · ", AllPermissions
            .Where(p => p != default && required.HasFlag(p))
            .Select(p => $"{Mark(effective.HasFlag(p))} {PermLabel(lang, p)}"));

    public static string PermissionListLabel(Language lang, Permissions permissions) =>
        permissions == 0 ? "—" : string.Join(", ", AllPermissions.Where(p => permissions.HasFlag(p)).Select(p => PermLabel(lang, p)));

    // Loads the live Discord snapshot both audits run against: the guild's channels/roles plus
    // the bot's own permission/hierarchy standing (via DiscordGuildDataService, so the Overview's
    // permission card shares exactly one source for that math). On a Discord failure the
    // channel/role lists come back empty with DiscordDataError set while the bot-status fields
    // keep the previous snapshot's values — the same partial state the page kept before this was
    // extracted (its catch only cleared channels/roles).
    public async Task<PermissionAuditContext> LoadContextAsync(ulong guildId, PermissionAuditContext previous, Language lang)
    {
        try
        {
            var channels = await discordData.GetChannelsAsync(guildId);

            // GetAllRolesAsync (not the filtered GetRolesAsync) — AllRolesById needs @everyone
            // too, for the bot permission/hierarchy math below.
            var allRoles = await discordData.GetAllRolesAsync(guildId);
            var allRolesById = allRoles.ToDictionary(r => r.Id);
            var roles = allRoles.Where(r => r.Id != guildId).ToList();

            var botUserId = ulong.Parse(configuration["Discord:ClientId"]!);
            var status = await discordData.GetBotRoleStatusAsync(guildId, botUserId);

            await using var db = await dbFactory.CreateDbContextAsync();
            var allianceTags = await db.GuildAlliances
                .Where(a => a.GuildId == guildId)
                .Select(a => new { a.Id, a.StfcAlliance.Tag })
                .ToDictionaryAsync(a => a.Id, a => a.Tag);

            return new PermissionAuditContext(
                guildId, channels, roles, allRolesById,
                status.BotMember, status.BotPermissions, status.HighestRolePosition,
                status.TopRoleId, status.TopRoleName, status.RolesAbove, status.NonAdminRolesAbove,
                DiscordDataError: null, AllianceTags: allianceTags);
        }
        catch (RestException)
        {
            return previous with
            {
                GuildId = guildId,
                Channels = [],
                Roles = [],
                DiscordDataError = Msg.WebCommon.DiscordLoadError(lang),
            };
        }
    }

    // Manual Re-check variant: evicts the 60s Discord cache first so a permission the admin just
    // changed in Discord is actually picked up — recomputing against the stale in-memory list is
    // exactly the "Re-check didn't reflect my change" symptom.
    public async Task<PermissionAuditContext> ReloadContextAsync(ulong guildId, PermissionAuditContext previous, Language lang)
    {
        discordData.InvalidateCache(guildId);
        return await LoadContextAsync(guildId, previous, lang);
    }

    public async Task<List<ChannelPermissionExpectation>> GetExpectationsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ChannelPermissionExpectations
            .Where(e => e.GuildId == guildId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task SaveExpectationAsync(ulong guildId, ExpectationSaveRequest request)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (request.Id is { } id)
        {
            var existing = await db.ChannelPermissionExpectations.FirstAsync(e => e.Id == id);
            existing.ChannelId = request.ChannelId;
            existing.RoleId = request.RoleId;
            existing.Allow = request.Allow;
            existing.Deny = request.Deny;
        }
        else
        {
            db.ChannelPermissionExpectations.Add(new ChannelPermissionExpectation
            {
                GuildId = guildId,
                ChannelId = request.ChannelId,
                RoleId = request.RoleId,
                Allow = request.Allow,
                Deny = request.Deny,
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task DeleteExpectationAsync(ChannelPermissionExpectation expectation)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.ChannelPermissionExpectations.Remove(expectation);
        await db.SaveChangesAsync();
    }

    // Diffs each expectation's Allow/Deny against the channel's live overwrite for that role.
    // Pure in-memory computation over the already-loaded context — no I/O.
    public List<PermissionAuditResult> AuditExpectations(
        PermissionAuditContext context, IReadOnlyList<ChannelPermissionExpectation> expectations, Language lang)
    {
        var results = new List<PermissionAuditResult>();
        foreach (var expectation in expectations)
        {
            var channel = context.Channels.FirstOrDefault(c => c.Id == expectation.ChannelId);

            PermissionOverwrite? overwrite = channel is not null && channel.PermissionOverwrites.TryGetValue(expectation.RoleId, out var found)
                ? found
                : null;

            var actualAllow = overwrite?.Allowed ?? default;
            var actualDeny = overwrite?.Denied ?? default;
            var expectedAllow = (Permissions)expectation.Allow;
            var expectedDeny = (Permissions)expectation.Deny;

            var missingAllow = AllPermissions.Where(p => expectedAllow.HasFlag(p) && !actualAllow.HasFlag(p)).ToList();
            var extraAllow = AllPermissions.Where(p => actualAllow.HasFlag(p) && !expectedAllow.HasFlag(p)).ToList();
            var missingDeny = AllPermissions.Where(p => expectedDeny.HasFlag(p) && !actualDeny.HasFlag(p)).ToList();
            var extraDeny = AllPermissions.Where(p => actualDeny.HasFlag(p) && !expectedDeny.HasFlag(p)).ToList();

            var isMatch = missingAllow.Count == 0 && extraAllow.Count == 0 && missingDeny.Count == 0 && extraDeny.Count == 0;
            var (canFix, blockReason) = isMatch ? (true, null) : EvaluateFixability(context, expectation, lang);
            results.Add(new PermissionAuditResult(expectation, isMatch, missingAllow, extraAllow, missingDeny, extraDeny, canFix, blockReason));
        }

        return results;
    }

    // Mirrors Discord's own rules for editing a channel permission overwrite: the bot
    // needs Manage Roles at all, its own highest role must outrank the role being
    // configured, and — unless it has Administrator — it can only Allow a permission it
    // already holds itself. Any of these failing makes "Fix" a guaranteed 403, so it's
    // checked here instead of just letting the API call fail.
    private static (bool CanFix, string? Reason) EvaluateFixability(
        PermissionAuditContext context, ChannelPermissionExpectation expectation, Language lang)
    {
        if (!context.BotHasManageRoles)
            return (false, Msg.WebAudit.BotLacksManageRoles(lang));

        if (context.BotPermissions.HasFlag(Permissions.Administrator))
            return (true, null);

        if (context.AllRolesById.TryGetValue(expectation.RoleId, out var targetRole) && targetRole.RawPosition >= context.BotHighestRolePosition)
            return (false, Msg.WebAudit.BotRoleOutranked(lang, context.BotTopRoleName, targetRole.Name));

        var expectedAllow = (Permissions)expectation.Allow;
        var ungrantable = AllPermissions.Where(p => expectedAllow.HasFlag(p) && !context.BotPermissions.HasFlag(p)).ToList();
        if (ungrantable.Count > 0)
            return (false, Msg.WebAudit.BotCannotGrant(lang, string.Join(", ", ungrantable)));

        return (true, null);
    }

    // Every channel the bot is configured to use, audited against what it actually has there.
    // Discovery (which channels, from which of the five storage shapes) is
    // BotChannelRequirementService's job and the required permissions come from the per-feature
    // declaration; this method is left with the Discord-side work — resolving effective
    // permissions, expanding configured categories, and deciding what can be fixed from here.
    public async Task<List<BotAccessResult>> CheckBotAccessAsync(PermissionAuditContext context, Language lang)
    {
        var requirements = ExpandCategories(await requirementService.LoadAsync(context.GuildId), context);
        var findings = ChannelAccessEvaluator.Evaluate(requirements, channelId =>
            context.Channels.FirstOrDefault(c => c.Id == channelId) is { } channel
                ? context.BotChannelPermissions(channel).ToDomain()
                : null);

        // The page shows one row per channel, so several features pointing at the same channel
        // collapse into one row whose requirement is the union of theirs — which is also exactly
        // what the Fix button has to grant, or fixing from one row would leave the others failing.
        var requiredByChannel = ChannelAccessEvaluator.RequiredByChannel(findings);

        var results = new List<BotAccessResult>();
        foreach (var group in findings.GroupBy(f => f.Requirement.ChannelId))
        {
            var channelId = group.Key;
            var sourceLabel = string.Join(", ", group
                .Select(f => SourceLabel(f.Requirement, context, lang))
                .Distinct()
                .OrderBy(l => l, StringComparer.OrdinalIgnoreCase));
            var req = requiredByChannel[channelId].ToNetCord();

            var channel = context.Channels.FirstOrDefault(c => c.Id == channelId);
            if (channel is null)
            {
                // Configured but not present in the live channel list — deleted, or the bot
                // can't even see it. Either way it's undeliverable and not auto-fixable here.
                results.Add(new BotAccessResult(channelId, null, sourceLabel, req, default, false, null, false, Msg.WebAudit.ChannelNotFound(lang)));
                continue;
            }

            var parentCategoryId = DiscordGuildDataService.GetParentCategoryId(channel);
            var channelEffective = context.BotChannelPermissions(channel);

            (Permissions Effective, bool Manage)? category = null;
            if (parentCategoryId is { } catId && context.Channels.FirstOrDefault(c => c.Id == catId) is { } categoryChannel)
            {
                var categoryEffective = context.BotChannelPermissions(categoryChannel);
                category = (categoryEffective, IsManage(categoryEffective));
            }

            var hasAccess = (req & ~channelEffective) == default;
            var (canFix, blockReason) = hasAccess ? (true, (string?)null) : EvaluateBotAccessFixability(context, req, lang);
            results.Add(new BotAccessResult(channelId, parentCategoryId, sourceLabel, req, channelEffective, IsManage(channelEffective), category, canFix, blockReason));
        }

        // Problems first, then alphabetically by what points at the channel.
        return results.OrderBy(r => r.HasAccess).ThenBy(r => r.Sources, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // A configured entry may be a whole category (the AI chat knowledge tiers), where the bot works
    // on the child channels rather than the category node — so audit the children it would actually
    // read. Text + forum only; forum children are checked at the forum level since threads inherit
    // the forum's read permissions. A category with no eligible children keeps its own row, or the
    // feature would silently look unconfigured.
    private static List<ChannelAccessRequirement> ExpandCategories(
        IReadOnlyList<ChannelAccessRequirement> requirements, PermissionAuditContext context)
    {
        var expanded = new List<ChannelAccessRequirement>();
        foreach (var requirement in requirements)
        {
            if (!requirement.CategoryExpands
                || context.Channels.FirstOrDefault(c => c.Id == requirement.ChannelId) is not CategoryGuildChannel category)
            {
                expanded.Add(requirement);
                continue;
            }

            var children = context.Channels
                .Where(c => DiscordGuildDataService.GetParentCategoryId(c) == category.Id && c is TextGuildChannel or ForumGuildChannel)
                .Select(c => requirement with { ChannelId = c.Id, ViaCategoryId = category.Id })
                .ToList();

            expanded.AddRange(children.Count > 0 ? children : [requirement]);
        }

        return expanded;
    }

    // What points at this channel, for the "Used by" column. Alliance-scoped rows carry the tag so
    // a coalition guild can tell three otherwise identical rows apart.
    private static string SourceLabel(ChannelAccessRequirement requirement, PermissionAuditContext context, Language lang)
    {
        var label = requirement switch
        {
            { Feature: null, Key: nameof(GuildChannelColumn.Log) } => Msg.WebGuild.LogTitle(lang),
            { Feature: null } => Msg.WebGuild.AdminTitle(lang),
            { Source: ChannelSlotSource.Setting, Feature: { } setting } => Msg.WebAudit.SourceFeatureSetting(lang, setting, requirement.Key),
            { Source: ChannelSlotSource.AlertChannel } => Msg.WebAudit.SourceAlert(lang, Enum.Parse<GuildAlertChannelKind>(requirement.Key)),
            { Source: ChannelSlotSource.AllianceColumn, Key: nameof(AllianceChannelColumn.StaffCommandBridge) } =>
                Msg.WebAudit.SourceStaffCommandBridge(lang, context.AllianceTag(requirement.GuildAllianceId)),
            { Source: ChannelSlotSource.AllianceColumn, Key: nameof(AllianceChannelColumn.FriendsCommandBridge) } =>
                Msg.WebAudit.SourceFriendsCommandBridge(lang, context.AllianceTag(requirement.GuildAllianceId)),
            { Source: ChannelSlotSource.AllianceColumn } => Msg.WebAudit.SourceCommandBridge(lang, context.AllianceTag(requirement.GuildAllianceId)),
            { Feature: { } feature } => Msg.WebAudit.SourceFeature(lang, feature),
        };

        return requirement.ViaCategoryId is { } categoryId
            ? Msg.WebAudit.SourceCategoryChild(lang, label, context.ChannelName(lang, categoryId))
            : label;
    }

    // The Fix grants the bot's own top role the required permissions via a channel overwrite,
    // so the same guardrails as EvaluateFixability apply: needs Manage Roles, and (unless
    // Administrator) can only grant permissions the bot already holds itself.
    private static (bool CanFix, string? Reason) EvaluateBotAccessFixability(
        PermissionAuditContext context, Permissions required, Language lang)
    {
        if (!context.BotHasManageRoles)
            return (false, Msg.WebAudit.BotLacksManageRoles(lang));

        if (context.BotPermissions.HasFlag(Permissions.Administrator))
            return (true, null);

        if (context.BotTopRoleId is null)
            return (false, Msg.WebAudit.BotNoRole(lang));

        var missing = AllPermissions
            .Where(p => p != default && required.HasFlag(p) && !context.BotPermissions.HasFlag(p))
            .Select(p => PermLabel(lang, p))
            .ToList();
        if (missing.Count > 0)
            return (false, Msg.WebAudit.BotCannotGrant(lang, string.Join(", ", missing)));

        return (true, null);
    }

    // One fix step. targetId is either the channel itself or its parent category: a category
    // grant fixes every channel under it that syncs its permissions in one go, a channel grant
    // touches only that channel. After applying, this re-reads live permissions and reports
    // whether the bot can now post — so the admin can escalate step by step (fix the category,
    // re-check, and only drop to the channel level if that channel doesn't sync). Returns null
    // when the bot has no assignable role to grant an overwrite to (nothing happened); otherwise
    // Applied says whether the overwrite went through — the caller only re-checks/replaces its
    // context when it did (a Discord refusal leaves the passed-in context untouched).
    public async Task<BotAccessFixOutcome?> FixBotAccessAsync(
        PermissionAuditContext context, BotAccessResult result, ulong targetId, bool viaCategory, Language lang)
    {
        if (context.BotTopRoleId is not { } roleId)
            return null;

        // Preserve any existing overwrite for the bot's role on the target; only add the
        // required permissions to Allow and clear them from Deny.
        Permissions existingAllow = default, existingDeny = default;
        if (context.Channels.FirstOrDefault(c => c.Id == targetId) is { } target
            && target.PermissionOverwrites.TryGetValue(roleId, out var overwrite))
        {
            existingAllow = overwrite.Allowed;
            existingDeny = overwrite.Denied;
        }

        try
        {
            await botRestClient.ModifyGuildChannelPermissionsAsync(targetId,
                new PermissionOverwriteProperties(roleId, PermissionOverwriteType.Role)
                {
                    Allowed = existingAllow | result.Required,
                    Denied = existingDeny & ~result.Required,
                });

            cache.Remove($"discord-guild-channels:{context.GuildId}");
            var reloaded = await LoadContextAsync(context.GuildId, context, lang);

            var channelNow = reloaded.Channels.FirstOrDefault(c => c.Id == result.ChannelId);
            var effectiveNow = channelNow is null ? default : reloaded.BotChannelPermissions(channelNow);
            var viewNow = effectiveNow.HasFlag(Permissions.ViewChannel);
            var hasAccessNow = (result.Required & ~effectiveNow) == default;
            var status = (viaCategory, hasAccessNow, viewNow) switch
            {
                (_, true, _) => viaCategory
                    ? Msg.WebAudit.FixedViaCategory(lang)
                    : Msg.WebAudit.FixedOnChannel(lang),
                (true, false, true) => Msg.WebAudit.CategoryUpdatedChannelMissing(lang),
                (true, false, false) => Msg.WebAudit.CategoryUpdatedNotSynced(lang),
                (false, false, _) => Msg.WebAudit.ChannelFixStillMissing(lang),
            };

            return new BotAccessFixOutcome(reloaded, status, Applied: true);
        }
        catch (RestException ex)
        {
            var level = viaCategory ? Msg.WebAudit.LevelCategory(lang) : Msg.WebAudit.LevelChannel(lang);
            var targetChannel = context.Channels.FirstOrDefault(c => c.Id == targetId);
            var canSeeTarget = targetChannel is not null && context.BotChannelPermissions(targetChannel).HasFlag(Permissions.ViewChannel);
            var status = ex.StatusCode == HttpStatusCode.Forbidden
                ? canSeeTarget
                    ? Msg.WebAudit.FixRefusedVisible(lang, level)
                    : Msg.WebAudit.FixRefusedInvisible(lang, level)
                : Msg.WebAudit.FixRejected(lang, ex.StatusCode);

            return new BotAccessFixOutcome(context, status, Applied: false);
        }
    }

    // Applies an expectation's exact Allow/Deny as the channel overwrite for its role, then
    // reloads live Discord data so the caller re-audits against fresh state. On a Discord
    // refusal nothing is reloaded: the passed-in context comes back untouched with the error
    // line the page surfaces in its top alert.
    public async Task<(PermissionAuditContext Context, string? Error)> FixExpectationAsync(
        PermissionAuditContext context, ChannelPermissionExpectation expectation, Language lang)
    {
        try
        {
            await botRestClient.ModifyGuildChannelPermissionsAsync(expectation.ChannelId,
                new PermissionOverwriteProperties(expectation.RoleId, PermissionOverwriteType.Role)
                {
                    Allowed = (Permissions)expectation.Allow,
                    Denied = (Permissions)expectation.Deny,
                });

            cache.Remove($"discord-guild-channels:{context.GuildId}");
            return (await LoadContextAsync(context.GuildId, context, lang), null);
        }
        catch (RestException)
        {
            return (context, Msg.WebAudit.FixExpectationError(lang));
        }
    }
}

// The live Discord snapshot the audits (and the page's banner/labels) read: the guild's
// channels and roles plus the bot's own permission/hierarchy standing. Empty is the pre-load
// stand-in, matching the page's old pre-load field defaults. Roles excludes @everyone (it's a
// display list); AllRolesById includes it, for the permission/hierarchy math.
public sealed record PermissionAuditContext(
    ulong GuildId,
    List<IGuildChannel> Channels,
    List<Role> Roles,
    IReadOnlyDictionary<ulong, Role> AllRolesById,
    GuildUser? BotMember,
    Permissions BotPermissions,
    int BotHighestRolePosition,
    ulong? BotTopRoleId,
    string? BotTopRoleName,
    int BotRolesAbove,
    int BotNonAdminRolesAbove,
    string? DiscordDataError,
    // Tag per linked alliance, so an alliance-scoped row can say WHICH alliance it belongs to —
    // a coalition guild otherwise gets three identical-looking Command Bridge rows.
    IReadOnlyDictionary<int, string>? AllianceTags = null)
{
    public static readonly PermissionAuditContext Empty =
        new(0, [], [], new Dictionary<ulong, Role>(), null, default, 0, null, null, 0, 0, null);

    public string AllianceTag(int? guildAllianceId) =>
        guildAllianceId is { } id && AllianceTags?.TryGetValue(id, out var tag) == true ? tag : "?";

    public bool BotHasManageRoles =>
        BotPermissions.HasFlag(Permissions.Administrator) || BotPermissions.HasFlag(Permissions.ManageRoles);

    // Effective channel permissions for the bot, via NetCord's own resolver (base guild perms +
    // the channel's @everyone/role/member overwrites, Administrator bypassing all). A channel
    // synced with its category already carries the category's overwrites on itself, so this one
    // channel-scoped call reflects the true result without walking to the parent.
    public Permissions BotChannelPermissions(IGuildChannel channel) =>
        BotMember?.GetChannelPermissions(BotPermissions, channel) ?? BotPermissions;

    public static string ChannelLabel(IGuildChannel channel) => channel switch
    {
        VoiceGuildChannel or StageGuildChannel => $"🔊 {channel.Name}",
        ForumGuildChannel or MediaForumGuildChannel => $"📋 {channel.Name}",
        _ => $"# {channel.Name}",
    };

    public string ChannelName(Language lang, ulong id) =>
        Channels.FirstOrDefault(c => c.Id == id) is { } channel ? ChannelLabel(channel) : Msg.WebCommon.Unknown(lang, id);

    public string CategoryName(Language lang, ulong id) =>
        Channels.FirstOrDefault(c => c.Id == id)?.Name ?? Msg.WebCommon.Unknown(lang, id);

    public string RoleName(Language lang, ulong id) =>
        Roles.FirstOrDefault(r => r.Id == id)?.Name ?? Msg.WebCommon.Unknown(lang, id);
}

// One expectation's audit outcome — the live overwrite diffed against its Allow/Deny.
public sealed record PermissionAuditResult(
    ChannelPermissionExpectation Expectation,
    bool IsMatch,
    List<Permissions> MissingAllow,
    List<Permissions> ExtraAllow,
    List<Permissions> MissingDeny,
    List<Permissions> ExtraDeny,
    bool CanFix,
    string? BlockReason);

// One configured channel and whether the bot has the permissions it actually needs there.
// Sources is the human-readable list of what points here; Required is the union of what those
// uses need (most just post → View + Send; the Announcements draft is read → also Read Message
// History). ChannelEffective/Category.Effective are the bot's resolved permissions on the
// channel and its parent category, shown side by side so a sync mismatch is visible.
public sealed record BotAccessResult(
    ulong ChannelId, ulong? ParentCategoryId, string Sources,
    Permissions Required,
    Permissions ChannelEffective, bool ChannelManage,
    (Permissions Effective, bool Manage)? Category,
    bool CanFix, string? BlockReason)
{
    public bool CanView => ChannelEffective.HasFlag(Permissions.ViewChannel);
    public Permissions ChannelMissing => Required & ~ChannelEffective;
    public bool HasAccess => ChannelMissing == default;

    // A fix at a given level only works if the bot can both see it and manage its
    // permissions there — Discord 403s an overwrite edit otherwise. So a button is offered
    // only where both hold; anything else needs a manual grant in Discord.
    public bool CanFixChannel => CanView && ChannelManage;

    // Category fix is also pointless when the category already grants everything required: the
    // child is blocked by its own (unsynced) overwrite, so re-granting on the category changes
    // nothing. Only offer it when the category can be managed and is itself still missing part
    // of what's required.
    public bool CanFixCategory => Category is { } cat
        && cat.Manage
        && cat.Effective.HasFlag(Permissions.ViewChannel)
        && (Required & ~cat.Effective) != default;
}

// See FixBotAccessAsync — Context is the post-fix reload when Applied, the caller's own
// unchanged context when Discord refused; Status is the per-channel outcome line the page
// shows under the row.
public sealed record BotAccessFixOutcome(PermissionAuditContext Context, string Status, bool Applied);

// What the expectation editor form hands back on save — Id null means "add new".
public sealed record ExpectationSaveRequest(int? Id, ulong ChannelId, ulong RoleId, ulong Allow, ulong Deny);
