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
public class DiscordGuildDataService(RestClient botRestClient, IMemoryCache cache)
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
    // outright; Announcement is allowed only for Normal, since private-thread creation (used by
    // Tickets) isn't supported there.
    public static bool IsAllowedChannel(IGuildChannel channel, ChannelKind kind) => kind switch
    {
        ChannelKind.Forum => channel is ForumGuildChannel or MediaForumGuildChannel,
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
    public async Task<ulong?> EnsureChannelAsync(ulong guildId, string? currentInput, string defaultName, ulong? categoryId)
    {
        if (ulong.TryParse(currentInput, out var existingId))
            return existingId;

        var created = await botRestClient.CreateGuildChannelAsync(guildId,
            new GuildChannelProperties(defaultName, ChannelType.TextGuildChannel) { ParentId = categoryId });
        InvalidateCache(guildId);
        return created.Id;
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
    }
}

// See GroupChannelsForDisplay — Category is null for a channel with no matching category.
public sealed record ChannelGroup(CategoryGuildChannel? Category, List<IGuildChannel> Channels);

// See IsAllowedChannel. Normal covers most settings (anything SendMessageAsync can target);
// TextOnly is for settings that create a private thread under the configured channel (Tickets),
// which Discord doesn't support on Announcement channels; Forum is for settings that post as a
// forum thread (RoE Violations — see RoeViolationService.CreateReportAsync).
public enum ChannelKind
{
    Normal,
    TextOnly,
    Forum,
}
