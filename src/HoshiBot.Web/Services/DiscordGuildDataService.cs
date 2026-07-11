using HoshiBot.Web.Components.Shared;
using Microsoft.Extensions.Caching.Memory;
using NetCord;
using NetCord.Rest;

namespace HoshiBot.Web.Services;

// Shared, 60s-cached fetch of a guild's live Discord channels/roles — was copy-pasted
// across Settings.razor, SetupWizard.razor, and ScopeEditor.razor with the same cache keys;
// this is that logic in one place instead of three. Every page that needs a channel/role
// picker depends on this now.
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
        var allRoles = await cache.GetOrCreateAsync($"discord-guild-roles:{guildId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await botRestClient.GetGuildRolesAsync(guildId);
        });

        return (allRoles ?? [])
            .Where(r => r.Id != guildId)
            .OrderByDescending(r => r.RawPosition)
            .ToList();
    }

    // Resolves a RolePicker's raw input to a role ID for the common "one role, no color/
    // mentionable" case — reuses an existing role by ID unchanged, or creates a new one named
    // defaultName when the picker's create option was chosen (RolePicker.CreateSentinel). Any
    // other input (a genuinely blank selection) returns null, clearing the setting — unlike
    // RankRoles/OpsLevelRoles, most single-role settings are optional and have no color/
    // mentionable inputs of their own, so those two features keep their own richer
    // EnsureRoleAsync instead of using this one.
    public async Task<ulong?> EnsureRoleAsync(ulong guildId, string? currentInput, string defaultName)
    {
        if (currentInput == RolePicker.CreateSentinel)
        {
            var created = await botRestClient.CreateGuildRoleAsync(guildId, new RoleProperties { Name = defaultName });
            InvalidateCache(guildId);
            return created.Id;
        }

        return ulong.TryParse(currentInput, out var id) ? id : null;
    }

    // Called after creating a channel/role on Discord so the next read reflects it
    // immediately instead of waiting out the 60s cache window.
    public void InvalidateCache(ulong guildId)
    {
        cache.Remove($"discord-guild-channels:{guildId}");
        cache.Remove($"discord-guild-roles:{guildId}");
    }
}
