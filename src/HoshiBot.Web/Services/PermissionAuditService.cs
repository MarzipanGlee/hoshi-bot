using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NetCord;
using NetCord.Rest;

namespace HoshiBot.Web.Services;

// The Permission Check page's two audit engines (expectation audit + bot posting access) and
// their fix paths, extracted from PermissionCheck.razor — components stay UI/state glue, the
// Discord REST/EF work and the pure permission math live here. The page (and its section
// child components) bind the result records this service computes.
public sealed class PermissionAuditService(
    IDbContextFactory<HoshiBotDbContext> dbFactory,
    DiscordGuildDataService discordData,
    RestClient botRestClient,
    IMemoryCache cache,
    IConfiguration configuration)
{
    // The bot's messages are branded embeds (EmbedBranding) almost everywhere, and sending or
    // editing a rich embed needs Embed Links — a channel that grants Send but denies Embed Links
    // (common where @everyone is locked down) would pass a View+Send check yet fail at post time.
    private const Permissions PostPermissions = Permissions.ViewChannel | Permissions.SendMessages | Permissions.EmbedLinks;

    // The Announcements draft channel is read (staff post drafts, the bot reads them back to
    // publish) as well as written — View + Send + Read Message History, but no embeds posted there.
    private const Permissions DraftPermissions = Permissions.ViewChannel | Permissions.SendMessages | Permissions.ReadMessageHistory;

    // AiChat listen channels: the bot reads recent messages back (Read Message History — this is
    // what lets it see any content at all) and posts plain-text replies (Send, no embeds). A
    // listen channel missing Read Message History is the classic "the bot stays silent for no
    // obvious reason" case.
    private const Permissions AiChatListenPermissions = Permissions.ViewChannel | Permissions.SendMessages | Permissions.ReadMessageHistory;

    // AiChat knowledge-source channels are only ever read, never posted to — View + Read Message
    // History. (A knowledge entry can also be a whole category; granting these on the category
    // propagates to its synced child channels.)
    private const Permissions AiChatKnowledgePermissions = Permissions.ViewChannel | Permissions.ReadMessageHistory;

    // The Territory Capture digest channel also gets its weekly message pinned (see
    // TerritoryCaptureDigestService.SendDigestAsync) — Manage Messages on top of the normal post
    // permissions. Missing it doesn't block the digest from posting, but silently fails the pin
    // and (before that failure was isolated from the send) once caused the whole digest to be
    // treated as failed and resent every 30 minutes, double-pinging the alliance's role.
    private const Permissions TerritoryCaptureDigestPermissions = PostPermissions | Permissions.ManageMessages;

    public static readonly Permissions[] AllPermissions = Enum.GetValues<Permissions>();

    private static bool IsManage(Permissions perms) =>
        perms.HasFlag(Permissions.Administrator) || perms.HasFlag(Permissions.ManageRoles);

    private static string Mark(bool granted) => granted ? "✅" : "❌";

    private static string PermLabel(Permissions permission) => permission switch
    {
        Permissions.ViewChannel => "View Channel",
        Permissions.SendMessages => "Send Messages",
        Permissions.EmbedLinks => "Embed Links",
        Permissions.ReadMessageHistory => "Read Message History",
        Permissions.AddReactions => "Add Reactions",
        Permissions.ManageMessages => "Manage Messages",
        _ => permission.ToString(),
    };

    // Each individual required permission with its granted state, e.g. "✅ View Channel · ❌ Send Messages".
    public static string PermsSummary(Permissions required, Permissions effective) =>
        string.Join(" · ", AllPermissions
            .Where(p => p != default && required.HasFlag(p))
            .Select(p => $"{Mark(effective.HasFlag(p))} {PermLabel(p)}"));

    public static string PermissionListLabel(Permissions permissions) =>
        permissions == 0 ? "—" : string.Join(", ", AllPermissions.Where(p => permissions.HasFlag(p)));

    // Loads the live Discord snapshot both audits run against: the guild's channels/roles plus
    // the bot's own permission/hierarchy standing (via DiscordGuildDataService, so the Overview's
    // permission card shares exactly one source for that math). On a Discord failure the
    // channel/role lists come back empty with DiscordDataError set while the bot-status fields
    // keep the previous snapshot's values — the same partial state the page kept before this was
    // extracted (its catch only cleared channels/roles).
    public async Task<PermissionAuditContext> LoadContextAsync(ulong guildId, PermissionAuditContext previous)
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

            return new PermissionAuditContext(
                guildId, channels, roles, allRolesById,
                status.BotMember, status.BotPermissions, status.HighestRolePosition,
                status.TopRoleId, status.TopRoleName, status.RolesAbove, status.NonAdminRolesAbove,
                DiscordDataError: null);
        }
        catch (RestException)
        {
            return previous with
            {
                GuildId = guildId,
                Channels = [],
                Roles = [],
                DiscordDataError = "Could not load channels/roles from Discord (is the bot in this guild?)",
            };
        }
    }

    // Manual Re-check variant: evicts the 60s Discord cache first so a permission the admin just
    // changed in Discord is actually picked up — recomputing against the stale in-memory list is
    // exactly the "Re-check didn't reflect my change" symptom.
    public async Task<PermissionAuditContext> ReloadContextAsync(ulong guildId, PermissionAuditContext previous)
    {
        discordData.InvalidateCache(guildId);
        return await LoadContextAsync(guildId, previous);
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
        PermissionAuditContext context, IReadOnlyList<ChannelPermissionExpectation> expectations)
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
            var (canFix, blockReason) = isMatch ? (true, null) : EvaluateFixability(context, expectation);
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
        PermissionAuditContext context, ChannelPermissionExpectation expectation)
    {
        if (!context.BotHasManageRoles)
            return (false, "Bot lacks Manage Roles permission in this server.");

        if (context.BotPermissions.HasFlag(Permissions.Administrator))
            return (true, null);

        if (context.AllRolesById.TryGetValue(expectation.RoleId, out var targetRole) && targetRole.RawPosition >= context.BotHighestRolePosition)
            return (false, $"Bot's role ({context.BotTopRoleName}) doesn't outrank {targetRole.Name} — move the bot's role higher.");

        var expectedAllow = (Permissions)expectation.Allow;
        var ungrantable = AllPermissions.Where(p => expectedAllow.HasFlag(p) && !context.BotPermissions.HasFlag(p)).ToList();
        if (ungrantable.Count > 0)
            return (false, $"Bot doesn't hold these permissions itself, so it can't grant them: {string.Join(", ", ungrantable)}.");

        return (true, null);
    }

    // The read-only knowledge buckets (AiChatKnowledge is the Normal tier); a configured entry here
    // may be a whole category that the bot expands to its child channels at read time.
    private static bool IsKnowledgeFeature(GuildFeature feature) => feature is
        GuildFeature.AiChatKnowledge or GuildFeature.AiChatKnowledgePreferred or GuildFeature.AiChatKnowledgeLastResort;

    // Every channel the bot is configured to post to (feature channel lists, alert channel
    // rows, and the guild-wide GuildSettings slots), audited for the bot's own View Channel +
    // Send Messages access — the check that would have surfaced the "message only reached one
    // channel" ClientRelease case (a second channel the bot silently couldn't post to).
    public async Task<List<BotAccessResult>> CheckBotAccessAsync(PermissionAuditContext context)
    {
        var guildId = context.GuildId;
        var sources = new Dictionary<ulong, SortedSet<string>>();
        var required = new Dictionary<ulong, Permissions>();
        void Add(ulong? id, string label, Permissions perms)
        {
            if (id is not { } channelId || channelId == 0)
                return;
            if (!sources.TryGetValue(channelId, out var set))
                sources[channelId] = set = [];
            set.Add(label);
            required[channelId] = required.GetValueOrDefault(channelId) | perms;
        }

        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            var featureChannels = await db.GuildFeatureChannels
                .Where(c => c.GuildId == guildId)
                .Select(c => new { c.ChannelId, c.Feature })
                .ToListAsync();
            foreach (var fc in featureChannels)
            {
                // Most features post embeds; AiChat instead reads message history (and, for
                // its listen channels, posts plain-text replies) — so it needs Read Message
                // History rather than Embed Links. All three knowledge buckets are read-only.
                var perms = fc.Feature switch
                {
                    GuildFeature.AiChat => AiChatListenPermissions,
                    GuildFeature.AiChatKnowledge
                        or GuildFeature.AiChatKnowledgePreferred
                        or GuildFeature.AiChatKnowledgeLastResort => AiChatKnowledgePermissions,
                    _ => PostPermissions,
                };
                var label = $"Feature: {fc.Feature}";

                // A knowledge entry can be a whole category; the bot reads its child channels
                // (AiChatIndexService.ResolveSourcesAsync), not the category node — so expand a
                // configured category to the children the bot would actually read (text + forum;
                // skip voice/stage/nested categories) and check each. Forum children are checked
                // at the forum level, since threads inherit the forum's read permissions.
                if (IsKnowledgeFeature(fc.Feature)
                    && context.Channels.FirstOrDefault(c => c.Id == fc.ChannelId) is CategoryGuildChannel category)
                {
                    foreach (var child in context.Channels.Where(c =>
                        DiscordGuildDataService.GetParentCategoryId(c) == category.Id
                        && c is TextGuildChannel or ForumGuildChannel))
                    {
                        Add(child.Id, $"{label} (category {category.Name})", perms);
                    }
                }
                else
                {
                    Add(fc.ChannelId, label, perms);
                }
            }

            var alertChannels = await db.GuildAlertChannels
                .Where(c => c.GuildId == guildId)
                .Select(c => new { c.ChannelId, c.Kind })
                .ToListAsync();
            foreach (var ac in alertChannels)
                Add(ac.ChannelId, $"Alert: {ac.Kind}", PostPermissions);

            // Per-feature channel settings (Absences report channels, Territory Capture
            // digest, Announcements/Tickets/etc.) live in the generic settings table, not a
            // dedicated channel table — by convention every channel-typed key ends in
            // "Channel" (role keys end in "Role", message-id keys in "MessageId"), so that's
            // how we tell a channel value apart from the roles stored alongside it. Most are
            // post targets; the Announcements draft is also read back by the bot, so it needs
            // Read Message History on top of View + Send.
            var settingChannels = await db.GuildFeatureSettingSnowflakes
                .Where(s => s.GuildId == guildId && s.Key.EndsWith("Channel"))
                .Select(s => new { s.Feature, s.Key, s.Value })
                .ToListAsync();
            foreach (var sc in settingChannels)
            {
                var perms = sc switch
                {
                    { Feature: GuildFeature.Announcements, Key: AnnouncementsSettingKeys.DraftChannel } => DraftPermissions,
                    { Feature: GuildFeature.TerritoryCapture, Key: TerritoryCaptureSettingKeys.DigestChannel } => TerritoryCaptureDigestPermissions,
                    _ => PostPermissions,
                };
                Add(sc.Value, $"Feature: {sc.Feature} ({sc.Key})", perms);
            }

            var settings = await db.GuildSettings.AsNoTracking().FirstOrDefaultAsync(s => s.GuildId == guildId);
            if (settings is not null)
            {
                Add(settings.LogChannelId, "Log", PostPermissions);
                Add(settings.AdminChannelId, "Admin", PostPermissions);
                Add(settings.UserLogChannelId, "User Log", PostPermissions);
            }

            // Alliance-scoped channels + Command Bridge now live per linked alliance.
            var allianceChannels = await db.GuildAlliances
                .Where(a => a.GuildId == guildId)
                .Include(a => a.StfcAlliance)
                .ToListAsync();
            foreach (var a in allianceChannels)
            {
                var tag = a.StfcAlliance.Tag;
                Add(a.AllianceBoardingChannelId, $"[{tag}] Alliance Boarding", PostPermissions);
                Add(a.CommandBridgeChannelId, $"[{tag}] Command Bridge", PostPermissions);
                Add(a.StaffCommandBridgeChannelId, $"[{tag}] Staff Command Bridge", PostPermissions);
                Add(a.FriendsCommandBridgeChannelId, $"[{tag}] Friends Command Bridge", PostPermissions);
                Add(a.RemindersAlliesChannelId, $"[{tag}] Reminders (Allies)", PostPermissions);
                // Reminders (Services) moved to the TerritoryCapture feature settings (ServicesChannel);
                // the generic per-feature-Channel loop above already covers it.
                Add(a.RulesDeChannelId, $"[{tag}] Rules (DE)", PostPermissions);
                Add(a.RulesEnChannelId, $"[{tag}] Rules (EN)", PostPermissions);
                Add(a.UserNotificationsChannelId, $"[{tag}] User Notifications", PostPermissions);
                Add(a.BotSupportChannelId, $"[{tag}] Bot Support", PostPermissions);
                Add(a.CommandStaffJobsChannelId, $"[{tag}] Command Staff Jobs", PostPermissions);
            }
        }

        var results = new List<BotAccessResult>();
        foreach (var (channelId, labels) in sources)
        {
            var sourceLabel = string.Join(", ", labels);
            var req = required[channelId];
            var channel = context.Channels.FirstOrDefault(c => c.Id == channelId);
            if (channel is null)
            {
                // Configured but not present in the live channel list — deleted, or the bot
                // can't even see it. Either way it's undeliverable and not auto-fixable here.
                results.Add(new BotAccessResult(channelId, null, sourceLabel, req, default, false, null, false, "Channel not found in this guild (deleted, or not visible to the bot)."));
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
            var (canFix, blockReason) = hasAccess ? (true, (string?)null) : EvaluateBotAccessFixability(context, req);
            results.Add(new BotAccessResult(channelId, parentCategoryId, sourceLabel, req, channelEffective, IsManage(channelEffective), category, canFix, blockReason));
        }

        // Problems first, then alphabetically by what points at the channel.
        return results.OrderBy(r => r.HasAccess).ThenBy(r => r.Sources, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // The Fix grants the bot's own top role the required permissions via a channel overwrite,
    // so the same guardrails as EvaluateFixability apply: needs Manage Roles, and (unless
    // Administrator) can only grant permissions the bot already holds itself.
    private static (bool CanFix, string? Reason) EvaluateBotAccessFixability(
        PermissionAuditContext context, Permissions required)
    {
        if (!context.BotHasManageRoles)
            return (false, "Bot lacks Manage Roles permission in this server.");

        if (context.BotPermissions.HasFlag(Permissions.Administrator))
            return (true, null);

        if (context.BotTopRoleId is null)
            return (false, "Bot has no assignable role to grant a channel override to.");

        var missing = AllPermissions
            .Where(p => p != default && required.HasFlag(p) && !context.BotPermissions.HasFlag(p))
            .Select(PermLabel)
            .ToList();
        if (missing.Count > 0)
            return (false, $"Bot doesn't hold these permissions itself, so it can't grant them: {string.Join(", ", missing)}.");

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
        PermissionAuditContext context, BotAccessResult result, ulong targetId, bool viaCategory)
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
            var reloaded = await LoadContextAsync(context.GuildId, context);

            var channelNow = reloaded.Channels.FirstOrDefault(c => c.Id == result.ChannelId);
            var effectiveNow = channelNow is null ? default : reloaded.BotChannelPermissions(channelNow);
            var viewNow = effectiveNow.HasFlag(Permissions.ViewChannel);
            var hasAccessNow = (result.Required & ~effectiveNow) == default;
            var status = (viaCategory, hasAccessNow, viewNow) switch
            {
                (_, true, _) => viaCategory
                    ? "✅ Fixed via the category — the bot now has the access it needs here."
                    : "✅ Fixed on the channel — the bot now has the access it needs here.",
                (true, false, true) => "Category updated, but the channel is still missing some of what it needs — use \"Fix on channel\".",
                (true, false, false) => "⚠ Category updated, but this channel doesn't inherit from it (its permissions aren't synced). A channel the bot can't see can't be fixed automatically — grant Hoshi Bot access to it manually in Discord.",
                (false, false, _) => "⚠ Overwrite applied on the channel, but the bot still lacks the required access. Check that its role isn't out-ranked and that another overwrite doesn't deny the needed permissions.",
            };

            return new BotAccessFixOutcome(reloaded, status, Applied: true);
        }
        catch (RestException ex)
        {
            var level = viaCategory ? "category" : "channel";
            var targetChannel = context.Channels.FirstOrDefault(c => c.Id == targetId);
            var canSeeTarget = targetChannel is not null && context.BotChannelPermissions(targetChannel).HasFlag(Permissions.ViewChannel);
            var status = ex.StatusCode == HttpStatusCode.Forbidden
                ? canSeeTarget
                    ? $"⚠ Discord refused the change — the bot can see this {level} but isn't allowed to change its permissions here (it needs the Manage Permissions right on it). Grant Hoshi Bot access to this {level} manually in Discord."
                    : $"⚠ Discord refused the change (Missing Access) — the bot can't edit a {level} it can't see. Grant Hoshi Bot access to it manually in Discord."
                : $"⚠ Discord rejected the change ({ex.StatusCode}). Check the bot's Manage Roles permission and role position.";

            return new BotAccessFixOutcome(context, status, Applied: false);
        }
    }

    // Applies an expectation's exact Allow/Deny as the channel overwrite for its role, then
    // reloads live Discord data so the caller re-audits against fresh state. On a Discord
    // refusal nothing is reloaded: the passed-in context comes back untouched with the error
    // line the page surfaces in its top alert.
    public async Task<(PermissionAuditContext Context, string? Error)> FixExpectationAsync(
        PermissionAuditContext context, ChannelPermissionExpectation expectation)
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
            return (await LoadContextAsync(context.GuildId, context), null);
        }
        catch (RestException)
        {
            return (context, "Could not update the permission overwrite on Discord — does the bot have Manage Roles/Manage Channels permission in this server?");
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
    string? DiscordDataError)
{
    public static readonly PermissionAuditContext Empty =
        new(0, [], [], new Dictionary<ulong, Role>(), null, default, 0, null, null, 0, 0, null);

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

    public string ChannelName(ulong id) =>
        Channels.FirstOrDefault(c => c.Id == id) is { } channel ? ChannelLabel(channel) : $"⚠ Unbekannt ({id})";

    public string CategoryName(ulong id) =>
        Channels.FirstOrDefault(c => c.Id == id)?.Name ?? $"⚠ Unbekannt ({id})";

    public string RoleName(ulong id) =>
        Roles.FirstOrDefault(r => r.Id == id)?.Name ?? $"⚠ Unbekannt ({id})";
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
