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
    public async Task<List<IGuildChannel>> GetChannelsAsync(ulong guildId)
    {
        var allChannels = await cache.GetOrCreateAsync($"discord-guild-channels:{guildId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await botRestClient.GetGuildChannelsAsync(guildId);
        });

        return (allChannels ?? [])
            .Where(c => c is not CategoryGuildChannel)
            .OrderBy(c => c.Position ?? int.MaxValue)
            .ThenBy(c => c.Name)
            .ToList();
    }

    public async Task<List<CategoryGuildChannel>> GetCategoriesAsync(ulong guildId)
    {
        var allChannels = await cache.GetOrCreateAsync($"discord-guild-channels:{guildId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await botRestClient.GetGuildChannelsAsync(guildId);
        });

        return (allChannels ?? [])
            .OfType<CategoryGuildChannel>()
            .OrderBy(c => c.Position ?? int.MaxValue)
            .ThenBy(c => c.Name)
            .ToList();
    }

    public async Task<List<Role>> GetRolesAsync(ulong guildId)
    {
        var allRoles = await GetCachedRolesAsync(guildId);
        return allRoles
            .Where(r => r.Id != guildId)
            .OrderByDescending(r => r.RawPosition)
            .ToList();
    }

    // Includes @everyone (whose Id equals guildId) — GetRolesAsync above excludes it since
    // every role-picker caller just wants a real, assignable-role display list. PermissionCheck
    // needs the raw list instead, to compute the bot's effective permissions/role hierarchy,
    // which are partly derived from @everyone's own base permissions.
    public async Task<List<Role>> GetAllRolesAsync(ulong guildId)
    {
        var allRoles = await GetCachedRolesAsync(guildId);
        return allRoles.OrderByDescending(r => r.RawPosition).ToList();
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
