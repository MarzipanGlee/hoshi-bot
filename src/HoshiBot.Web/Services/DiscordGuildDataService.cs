using HoshiBot.Domain.Localization;
using HoshiBot.Web.Components.Shared;
using Microsoft.Extensions.Caching.Memory;
using NetCord;
using NetCord.Rest;

namespace HoshiBot.Web.Services;

// Every Razor component that reads or mutates a guild's live Discord channels/roles goes
// through here instead of injecting RestClient/IMemoryCache directly — keeps components as
// UI/state glue and the actual Discord REST calls (plus their 60s response cache) in one
// place. Components with a Discord need this doesn't cover (e.g. PermissionCheck.razor's bot-
// member/permission-modify calls, genuinely specific to that one page) still inject RestClient
// directly — this service is for the common "list/create/reuse a channel or role" cases.
public partial class DiscordGuildDataService(RestClient botRestClient, IMemoryCache cache, IConfiguration configuration)
{
    // Discord's real channel list order: categories and uncategorized channels share one
    // position ordering at the guild's root level (a category and a "no parent" channel are
    // siblings there); each category's own children are ordered separately, by their own
    // position within that category. Categories are included in the result (not just real
    // channels) rather than excluded — GroupChannelsForDisplay below uses that to build real
    // <optgroup> nesting for a <select>.
    public async Task<List<IGuildChannel>> GetChannelsAsync(ulong guildId)
    {
        var allChannels = await GetCachedChannelsAsync(guildId);

        var categories = allChannels.OfType<CategoryGuildChannel>()
            .OrderBy(c => c.Position ?? int.MaxValue)
            .ThenBy(c => c.Name)
            .ToList();

        var childrenByParentId = allChannels
            .Where(c => c is not CategoryGuildChannel)
            .ToLookup(GetParentCategoryId);

        var rootEntries = categories.Cast<IGuildChannel>()
            .Concat(childrenByParentId[null])
            .OrderBy(c => c.Position ?? int.MaxValue)
            .ThenBy(c => c.Name);

        var ordered = new List<IGuildChannel>();
        foreach (var entry in rootEntries)
        {
            ordered.Add(entry);
            if (entry is CategoryGuildChannel category)
            {
                ordered.AddRange(childrenByParentId[category.Id]
                    .OrderBy(c => c.Position ?? int.MaxValue)
                    .ThenBy(c => c.Name));
            }
        }

        return ordered;
    }

    public async Task<List<CategoryGuildChannel>> GetCategoriesAsync(ulong guildId)
    {
        var allChannels = await GetCachedChannelsAsync(guildId);
        return allChannels
            .OfType<CategoryGuildChannel>()
            .OrderBy(c => c.Position ?? int.MaxValue)
            .ThenBy(c => c.Name)
            .ToList();
    }

    // Every nestable channel type exposes its own ParentId — there's no shared interface for it
    // (confirmed via reflection against the installed NetCord package). Only two arms needed:
    // Voice/Stage/Announcement all derive from TextGuildChannel (inheriting its ParentId), and
    // MediaForum derives from ForumGuildChannel — matching the base type already covers them.
    // Public so the <select>-rendering call sites below can use the real parent/child
    // relationship too, not just position-based ordering.
    public static ulong? GetParentCategoryId(IGuildChannel channel) => channel switch
    {
        TextGuildChannel c => c.ParentId,
        ForumGuildChannel c => c.ParentId,
        _ => null,
    };

    // Voice/Stage/Announcement all derive from TextGuildChannel in NetCord's model (they share
    // its shape — topic, ParentId, etc. — even though they're not plain text channels), so a
    // plain `is TextGuildChannel` check would wrongly match them too. Voice/Stage are excluded
    // outright; Announcement is allowed only for Normal (and NormalOrForum), since private-thread
    // creation (used by Tickets) isn't supported there. NormalOrForum additionally keeps forums:
    // it's for settings the bot only ever READS from (the AI-chat knowledge sources, which expand
    // a forum to its threads — see AiChatIndexService.AddSourceAsync), where "can the bot post
    // here" — the reason every other kind drops forums — simply doesn't apply.
    public static bool IsAllowedChannel(IGuildChannel channel, ChannelKind kind) => kind switch
    {
        ChannelKind.Forum => channel is ForumGuildChannel or MediaForumGuildChannel,
        ChannelKind.NormalOrForum => channel is ForumGuildChannel or MediaForumGuildChannel
            || IsAllowedChannel(channel, ChannelKind.Normal),
        _ => channel switch
        {
            VoiceGuildChannel or StageGuildChannel or ForumGuildChannel or MediaForumGuildChannel => false,
            AnnouncementGuildChannel => kind == ChannelKind.Normal,
            TextGuildChannel => true,
            _ => false,
        },
    };

    // Rendering-only view over an already-ordered GetChannelsAsync result — groups each category
    // with the channels immediately following it that are genuinely its children (checked via
    // GetParentCategoryId, not just position), so a <select> can render real <optgroup> nesting
    // instead of a flat sequence. A channel with no matching category (before the first
    // category, or genuinely parentless) comes back as its own single-channel, Category-null
    // entry.
    public static List<ChannelGroup> GroupChannelsForDisplay(IReadOnlyList<IGuildChannel> orderedChannels)
    {
        var groups = new List<ChannelGroup>();
        foreach (var channel in orderedChannels)
        {
            if (channel is CategoryGuildChannel category)
            {
                groups.Add(new ChannelGroup(category, []));
            }
            else if (groups.Count > 0 && groups[^1].Category is { } lastCategory
                && GetParentCategoryId(channel) == lastCategory.Id)
            {
                groups[^1].Channels.Add(channel);
            }
            else
            {
                groups.Add(new ChannelGroup(null, [channel]));
            }
        }

        return groups;
    }

    // The guild's Discord preferred locale (e.g. "de", "en-US"), or null if it can't be read —
    // used to default the AI-chat full-text search language. Cached briefly like the channel list.
    public async Task<string?> GetPreferredLocaleAsync(ulong guildId)
    {
        return await cache.GetOrCreateAsync($"discord-guild-locale:{guildId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            try
            {
                var guild = await botRestClient.GetGuildAsync(guildId);
                return guild.PreferredLocale;
            }
            catch (RestException)
            {
                return null;
            }
        });
    }

    private async Task<IReadOnlyList<IGuildChannel>> GetCachedChannelsAsync(ulong guildId)
    {
        var allChannels = await cache.GetOrCreateAsync($"discord-guild-channels:{guildId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await botRestClient.GetGuildChannelsAsync(guildId);
        });

        return allChannels ?? [];
    }

    public async Task<List<Role>> GetRolesAsync(ulong guildId)
    {
        var allRoles = await GetCachedRolesAsync(guildId);
        return allRoles
            .Where(r => r.Id != guildId)
            .OrderByDescending(r => r.Position)
            .ToList();
    }

    // Includes @everyone (whose Id equals guildId) — GetRolesAsync above excludes it since
    // every role-picker caller just wants a real, assignable-role display list. PermissionCheck
    // needs the raw list instead, to compute the bot's effective permissions/role hierarchy,
    // which are partly derived from @everyone's own base permissions.
    public async Task<List<Role>> GetAllRolesAsync(ulong guildId)
    {
        var allRoles = await GetCachedRolesAsync(guildId);
        return allRoles.OrderByDescending(r => r.Position).ToList();
    }

    // The bot's own role/permission standing in a guild — its effective guild permissions (the OR
    // of @everyone plus every role it holds), its highest role, and how many roles outrank it.
    // Single source for both the Permission Check page's top banner and the Overview's permission
    // card. AllRolesById includes @everyone (needed for the permission/hierarchy math and for
    // PermissionCheck's per-channel fixability checks). BotMember is null only if Discord couldn't
    // be reached, in which case every count is 0 / TopRoleName is "@everyone".
    public sealed record BotGuildRoleStatus(
        GuildUser? BotMember,
        Permissions BotPermissions,
        int HighestRolePosition,
        ulong? TopRoleId,
        string TopRoleName,
        int RolesAbove,
        int NonAdminRolesAbove,
        IReadOnlyDictionary<ulong, Role> AllRolesById)
    {
        public bool HasManageRoles =>
            BotPermissions.HasFlag(Permissions.Administrator) || BotPermissions.HasFlag(Permissions.ManageRoles);
    }

    // Discord computes a member's effective guild permissions as the OR of @everyone's permissions
    // plus every explicit role they hold (RoleIds never includes @everyone itself, so it's added
    // separately). The bot member is cached 60s under the same key PermissionCheck uses. Throws
    // RestException on a Discord failure — callers wrap it (the Overview shows a fallback line).
    public async Task<BotGuildRoleStatus> GetBotRoleStatusAsync(ulong guildId, ulong botUserId)
    {
        var botMember = await cache.GetOrCreateAsync($"discord-bot-member:{guildId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await botRestClient.GetGuildUserAsync(guildId, botUserId);
        });

        var allRolesById = (await GetAllRolesAsync(guildId)).ToDictionary(r => r.Id);

        var botRoles = (botMember?.RoleIds ?? [])
            .Select(id => allRolesById.GetValueOrDefault(id))
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();

        // @everyone always applies to every member — Discord's guild-permission baseline.
        if (allRolesById.TryGetValue(guildId, out var everyoneRole))
            botRoles.Add(everyoneRole);

        var botPermissions = botRoles.Aggregate(default(Permissions), (acc, r) => acc | r.Permissions);

        var topRole = botRoles.OrderByDescending(r => r.RawPosition).FirstOrDefault();
        var highestRolePosition = topRole?.RawPosition ?? 0;
        var topRoleId = topRole?.Id;
        var topRoleName = topRole is { Id: var id } && id != guildId ? topRole.Name : "@everyone";

        // How many roles sit above the bot's highest — the "rank from the top" a user sees in
        // Server Settings → Roles. These are exactly the roles the bot can't manage. Roles above it
        // that hold Administrator don't matter (admin bypasses every channel overwrite), so only a
        // non-admin role above is a real concern.
        var rolesAbove = allRolesById.Values.Where(r => r.RawPosition > highestRolePosition).ToList();
        var nonAdminRolesAbove = rolesAbove.Count(r => !r.Permissions.HasFlag(Permissions.Administrator));

        return new BotGuildRoleStatus(
            botMember, botPermissions, highestRolePosition, topRoleId, topRoleName,
            rolesAbove.Count, nonAdminRolesAbove, allRolesById);
    }

    // userId → display label (nickname/global name, alliance-tag prefix stripped like the bot's
    // CommanderName.Of) for a guild's non-bot members — used by the member-lore notes/review UI to
    // label rows by name instead of raw ids. When two members share a display name (e.g. a person's
    // alt account), the unique Discord @username is appended so they stay distinguishable. Cached 60s.
    public async Task<IReadOnlyDictionary<ulong, string>> GetMemberDisplayNamesAsync(ulong guildId)
    {
        var names = await cache.GetOrCreateAsync($"discord-guild-member-names:{guildId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            var members = new List<(ulong Id, string Display, string Username)>();
            await foreach (var member in botRestClient.GetGuildUsersAsync(guildId))
            {
                if (member.IsBot)
                    continue;
                var display = AllianceTagPattern().Replace(member.Nickname ?? member.GlobalName ?? member.Username, "").Trim();
                members.Add((member.Id, string.IsNullOrEmpty(display) ? member.Username : display, member.Username));
            }

            // Append @username only where a display name is shared by more than one member.
            var shared = members.GroupBy(m => m.Display, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return members.ToDictionary(
                m => m.Id,
                m => shared.Contains(m.Display) ? $"{m.Display} (@{m.Username})" : m.Display);
        });

        return names ?? new Dictionary<ulong, string>();
    }

    // GetMemberDisplayNamesAsync wrapped in a MemberDirectory, so pages labeling rows by member get
    // the shared name-or-raw-id fallback instead of each re-implementing it.
    public async Task<MemberDirectory> GetMemberDirectoryAsync(ulong guildId) =>
        new(await GetMemberDisplayNamesAsync(guildId));

    // Like GetMemberDisplayNamesAsync but keeps the FULL nickname including any [TAG] prefix — used by
    // the Player Assignment page, where the admin wants to see each member's real Discord nickname.
    // How a member is labelled in an admin list: their full guild nickname — alliance tag and all,
    // which groups a list by alliance when sorted — followed by their Discord username in brackets.
    //
    // The username is what makes the label unique. Nicknames are not: alt accounts and common names
    // produce several visually identical rows, and a picker showing "[LF] MarzipanGlee" twice gives
    // an admin no way to tell which one they are about to write a note against. Discord usernames
    // are globally unique, so the pair always distinguishes.
    //
    // Always appended, even where the nickname already looks distinct — a label whose shape depends
    // on whether some other member happens to share a name is harder to scan, not easier.
    public async Task<IReadOnlyDictionary<ulong, string>> GetMemberLabelsAsync(ulong guildId)
    {
        var names = await cache.GetOrCreateAsync($"discord-guild-member-labels:{guildId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            var map = new Dictionary<ulong, string>();
            await foreach (var member in botRestClient.GetGuildUsersAsync(guildId))
            {
                if (member.IsBot)
                    continue;

                var display = member.Nickname ?? member.GlobalName ?? member.Username;
                map[member.Id] = display == member.Username ? display : $"{display} ({member.Username})";
            }
            return map;
        });

        return names ?? new Dictionary<ulong, string>();
    }

    // Name + avatar for arbitrary Discord accounts, with no guild involved — /me listing the other
    // accounts that belong to the same person, who may share no server with the one signed in.
    // GET /users/{id} works for any account with a bot token; a failure degrades to the raw id
    // rather than breaking the page.
    public async Task<IReadOnlyDictionary<ulong, DiscordUserSummary>> GetUserSummariesAsync(IEnumerable<ulong> userIds)
    {
        var result = new Dictionary<ulong, DiscordUserSummary>();
        foreach (var userId in userIds.Distinct())
        {
            var summary = await cache.GetOrCreateAsync($"discord-user:{userId}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                try
                {
                    var user = await botRestClient.GetUserAsync(userId);
                    return new DiscordUserSummary(user.GlobalName ?? user.Username, user.GetAvatarUrl()?.ToString());
                }
                catch (RestException)
                {
                    return new DiscordUserSummary(userId.ToString(), null);
                }
            });

            result[userId] = summary ?? new DiscordUserSummary(userId.ToString(), null);
        }
        return result;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\[.*\]\s*")]
    private static partial System.Text.RegularExpressions.Regex AllianceTagPattern();

    private async Task<IReadOnlyList<Role>> GetCachedRolesAsync(ulong guildId)
    {
        var allRoles = await cache.GetOrCreateAsync($"discord-guild-roles:{guildId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await botRestClient.GetGuildRolesAsync(guildId);
        });

        return allRoles ?? [];
    }

    // Resolves a RolePicker's raw input to a role ID for the common "one role, no color/
    // mentionable" case — reuses an existing role by ID unchanged, or creates a new one named
    // defaultName when the picker's create option was chosen (RolePicker.CreateSentinel). Any
    // other input (a genuinely blank selection) returns null, clearing the setting — most
    // single-role settings are optional this way. RankRoles/OpsLevelRoles/ScopeEditor's
    // alliance roles always need a role assigned and also manage color/mentionable, so they use
    // the richer EnsureRoleAsync overload below instead.
    //
    // alwaysCreateIfBlank is for SetupWizard's plain <select> — it has no RolePicker-style
    // choose/create split, a blank selection there always means "create it for me," so this
    // skips the CreateSentinel gate entirely rather than requiring one.
    public async Task<ulong?> EnsureRoleAsync(ulong guildId, string? currentInput, string defaultName, bool alwaysCreateIfBlank = false)
    {
        if (ulong.TryParse(currentInput, out var existingId))
            return existingId;

        if (!alwaysCreateIfBlank && currentInput != RolePicker.CreateSentinel)
            return null;

        var created = await botRestClient.CreateGuildRoleAsync(guildId, new RoleProperties { Name = defaultName });
        InvalidateCache(guildId);
        return created.Id;
    }

    // Richer sibling of the overload above — also manages color/mentionable, modifying an
    // existing role in place if either differs from desired instead of only ever creating new
    // ones. RankRoles/OpsLevelRoles/ScopeEditor's alliance roles all had their own copy of this
    // exact method (plus ToColor/ToHex below) before this was pulled out here. Unlike the
    // simple overload, any non-parseable currentInput (not just RolePicker.CreateSentinel)
    // creates a new role — these three callers always need a role assigned, with no "none"
    // state, matching their pre-existing behavior.
    public async Task<ulong?> EnsureRoleAsync(
        ulong guildId, string? currentInput, string defaultName, string? colorInput, bool mentionable, IReadOnlyList<Role> currentRoles)
    {
        var desiredColor = ToColor(colorInput);

        if (ulong.TryParse(currentInput, out var existingId))
        {
            var existingRole = currentRoles.FirstOrDefault(r => r.Id == existingId);
            if (existingRole is not null &&
                (existingRole.Colors.PrimaryColor.RawValue != desiredColor.RawValue || existingRole.Mentionable != mentionable))
            {
                await botRestClient.ModifyGuildRoleAsync(guildId, existingId, props =>
                {
                    props.Colors = new RoleColorsProperties(desiredColor);
                    props.Mentionable = mentionable;
                });
                InvalidateCache(guildId);
            }

            return existingId;
        }

        var created = await botRestClient.CreateGuildRoleAsync(guildId, new RoleProperties
        {
            Name = defaultName,
            Colors = new RoleColorsProperties(desiredColor),
            Mentionable = mentionable,
        });
        InvalidateCache(guildId);
        return created.Id;
    }

    // SetupWizard-only: creates a new category channel to group the rest of the wizard's
    // channels under. No "reuse existing" branch — the wizard only calls this when the admin
    // typed a new category name rather than picking an existing one.
    public async Task<ulong> CreateCategoryAsync(ulong guildId, string name)
    {
        var category = await botRestClient.CreateGuildChannelAsync(guildId, new GuildChannelProperties(name, ChannelType.CategoryChannel));
        InvalidateCache(guildId);
        return category.Id;
    }

    // SetupWizard-only sibling of EnsureRoleAsync, for a plain text channel instead of a role —
    // reuses an existing channel by ID unchanged, or creates a new one (optionally under
    // categoryId) when the wizard's blank "create it for me" selection was left as-is.
    // Overwrites for a channel only leadership may read. Three parts, and all three matter:
    //
    //   @everyone is denied — the whole point. A staff channel created with default permissions is
    //   world-readable, which for the staff absence report means publishing the absences members
    //   marked private. Creating it that way and hoping an admin notices is not acceptable.
    //
    //   The BOT is allowed explicitly. Denying @everyone denies the bot too unless it happens to
    //   hold Administrator, and a bot that cannot see the channel it was told to post in fails
    //   silently until someone reads the admin ping.
    //
    //   The staff role is allowed when the alliance has one. Without it the channel is admin-only,
    //   which is safe rather than useful — the admin grants access, and no absence leaks meanwhile.
    public IEnumerable<PermissionOverwriteProperties> StaffOnlyOverwrites(ulong guildId, ulong? staffRoleId)
    {
        // @everyone's role id is the guild id — Discord models it that way.
        var overwrites = new List<PermissionOverwriteProperties>
        {
            new PermissionOverwriteProperties(guildId, PermissionOverwriteType.Role) { Denied = Permissions.ViewChannel },
        };

        if (ulong.TryParse(configuration["Discord:ClientId"], out var botUserId))
        {
            overwrites.Add(new PermissionOverwriteProperties(botUserId, PermissionOverwriteType.User)
            {
                Allowed = Permissions.ViewChannel | Permissions.SendMessages | Permissions.ReadMessageHistory,
            });
        }

        if (staffRoleId is { } roleId)
        {
            overwrites.Add(new PermissionOverwriteProperties(roleId, PermissionOverwriteType.Role)
            {
                Allowed = Permissions.ViewChannel | Permissions.ReadMessageHistory,
            });
        }

        return overwrites;
    }

    public async Task<ulong?> EnsureChannelAsync(ulong guildId, string? currentInput, string defaultName, ulong? categoryId)
    {
        if (ulong.TryParse(currentInput, out var existingId))
            return existingId;

        var created = await botRestClient.CreateGuildChannelAsync(guildId,
            new GuildChannelProperties(defaultName, ChannelType.TextGuildChannel) { ParentId = categoryId });
        InvalidateCache(guildId);
        return created.Id;
    }

    // ChannelPicker counterpart of the simple EnsureRoleAsync: reuse an existing channel by ID,
    // create a channel/category of the given type when ChannelPicker's "create" sentinel was
    // picked, or return null (keep "none") for a genuinely blank selection. Categories ignore
    // categoryId (they have no parent); everything else nests under it when provided.
    public async Task<ulong?> EnsureChannelAsync(ulong guildId, string? currentInput, string defaultName, ChannelType type,
        ulong? categoryId = null, IEnumerable<PermissionOverwriteProperties>? overwrites = null)
    {
        if (ulong.TryParse(currentInput, out var existingId))
            return existingId;

        if (currentInput != ChannelPicker.CreateSentinel)
            return null;

        var properties = new GuildChannelProperties(defaultName, type);
        if (type != ChannelType.CategoryChannel)
            properties.ParentId = categoryId;

        if (overwrites is not null)
            properties.PermissionOverwrites = overwrites;

        var created = await botRestClient.CreateGuildChannelAsync(guildId, properties);
        InvalidateCache(guildId);
        return created.Id;
    }

    // EnsureRoleAsync wrapped in the create-failure handling every editor used to repeat inline:
    // on success Error is null and Input is the ensured id as picker input; on a RestException
    // Error carries the failure KIND (not a message — see DiscordCreateErrorKind) and Input
    // resets a failed *create* back to blank while keeping an existing selection (a failed
    // modify of an already-selected role shouldn't silently drop it). Callers deconstruct
    // straight into their own state:
    //   (var roleId, roleIdInput, createError) = await DiscordData.EnsureRoleOrErrorAsync(...);
    public async Task<(ulong? Id, string? Input, DiscordCreateErrorKind? Error)> EnsureRoleOrErrorAsync(
        ulong guildId, string? currentInput, string defaultName)
    {
        try
        {
            var id = await EnsureRoleAsync(guildId, currentInput, defaultName);
            return (id, id?.ToString(), null);
        }
        catch (RestException)
        {
            return (null, currentInput == RolePicker.CreateSentinel ? null : currentInput, DiscordCreateErrorKind.Role);
        }
    }

    // Same wrapper for the richer color/mentionable EnsureRoleAsync overload (RoleTierEditor).
    public async Task<(ulong? Id, string? Input, DiscordCreateErrorKind? Error)> EnsureRoleOrErrorAsync(
        ulong guildId, string? currentInput, string defaultName, string? colorInput, bool mentionable, IReadOnlyList<Role> currentRoles)
    {
        try
        {
            var id = await EnsureRoleAsync(guildId, currentInput, defaultName, colorInput, mentionable, currentRoles);
            return (id, id?.ToString(), null);
        }
        catch (RestException)
        {
            return (null, currentInput == RolePicker.CreateSentinel ? null : currentInput, DiscordCreateErrorKind.Role);
        }
    }

    // ChannelPicker counterpart — the kind says "category" when that's what was being
    // created, matching the text each page showed before this wrapper existed.
    public async Task<(ulong? Id, string? Input, DiscordCreateErrorKind? Error)> EnsureChannelOrErrorAsync(
        ulong guildId, string? currentInput, string defaultName, ChannelType type, ulong? categoryId = null,
        IEnumerable<PermissionOverwriteProperties>? overwrites = null)
    {
        try
        {
            var id = await EnsureChannelAsync(guildId, currentInput, defaultName, type, categoryId, overwrites);
            return (id, id?.ToString(), null);
        }
        catch (RestException)
        {
            return (null, currentInput == ChannelPicker.CreateSentinel ? null : currentInput,
                type == ChannelType.CategoryChannel ? DiscordCreateErrorKind.Category : DiscordCreateErrorKind.Channel);
        }
    }

    // null = Discord's own "Default" (raw color value 0) — can't just omit Colors when
    // modifying an existing role, since Discord's API treats an omitted field as "leave
    // unchanged," not "clear", so resetting to Default needs this explicit raw-0 value.
    public static NetCord.Color ToColor(string? colorInput)
    {
        if (colorInput is null)
            return new NetCord.Color(0);

        var hex = colorInput.TrimStart('#');
        // The picker may report an 8-digit RRGGBBAA (opacity support is on to make its
        // Swatches list render at all) — Discord role colors have no alpha, only the first
        // 6 digits matter.
        if (hex.Length > 6)
            hex = hex[..6];

        return new NetCord.Color(Convert.ToInt32(hex, 16));
    }

    // RawValue 0 is Discord's own "no custom color" — must round-trip back to null (not the
    // literal string "#000000") so a freshly-loaded default-colored role still shows the
    // Default button as pressed/active, instead of looking like real black was explicitly
    // picked.
    public static string? ToHex(NetCord.Color color) => color.RawValue == 0 ? null : $"#{color.RawValue:X6}";

    // Called after creating a channel/role on Discord so the next read reflects it
    // immediately instead of waiting out the 60s cache window.
    public void InvalidateCache(ulong guildId)
    {
        cache.Remove($"discord-guild-channels:{guildId}");
        cache.Remove($"discord-guild-roles:{guildId}");

        // The permission snapshot is derived from both of the above, so it has to go with them —
        // otherwise a Fix on the permission page leaves the feature badges showing the old state
        // for up to a minute. Referenced by prefix rather than by injecting the service, which
        // would be a cycle (the snapshot service reads channels/roles from here).
        cache.Remove($"{GuildPermissionSnapshotService.CacheKeyPrefix}{guildId}");
    }
}

// What the OrError wrappers report on a Discord create/modify failure. A KIND rather than a
// message so the service stays language-free (the localization plan's rule: nothing localized
// in shared services/caches) — the consuming component maps it to the viewer's language at
// render time via Text(Lang).
public enum DiscordCreateErrorKind
{
    Role,
    Channel,
    Category,
}

public static class DiscordCreateErrorKindExtensions
{
    public static string Text(this DiscordCreateErrorKind kind, Language lang) => kind switch
    {
        DiscordCreateErrorKind.Channel => Msg.WebCommon.CreateChannelError(lang),
        DiscordCreateErrorKind.Category => Msg.WebCommon.CreateCategoryError(lang),
        _ => Msg.WebCommon.CreateRoleError(lang),
    };
}

// See GroupChannelsForDisplay — Category is null for a channel with no matching category.
public sealed record ChannelGroup(CategoryGuildChannel? Category, List<IGuildChannel> Channels);

// See GetUserSummariesAsync. AvatarUrl is null for an account with no custom avatar (or when the
// lookup failed, in which case Name is the raw id) — callers render their own fallback.
public sealed record DiscordUserSummary(string Name, string? AvatarUrl);

// See GetMemberDirectoryAsync — a guild's member display names plus the shared fallback: an id with
// no (usable) name renders as the raw id string. Empty is the pre-load stand-in, so pages can render
// before the Discord fetch completes.
public sealed record MemberDirectory(IReadOnlyDictionary<ulong, string> Names)
{
    public static readonly MemberDirectory Empty = new(new Dictionary<ulong, string>());

    public string DisplayName(ulong id) =>
        Names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name) ? name : id.ToString();
}

// See IsAllowedChannel. Normal covers most settings (anything SendMessageAsync can target);
// TextOnly is for settings that create a private thread under the configured channel (Tickets),
// which Discord doesn't support on Announcement channels; Forum is for settings that post as a
// forum thread (RoE Violations — see RoeViolationService.CreateReportAsync); NormalOrForum is for
// read-only settings that never post (AI-chat knowledge sources).
public enum ChannelKind
{
    Normal,
    TextOnly,
    Forum,
    NormalOrForum,
}
