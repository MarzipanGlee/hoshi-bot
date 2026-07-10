using Microsoft.Extensions.Caching.Memory;
using NetCord;
using NetCord.Rest;

namespace HoshiBot.Web.Services;

// Shared, 60s-cached fetch of a guild's live Discord channels/roles — was copy-pasted
// across Settings.razor, Setup.razor, and ScopeEditor.razor with the same cache keys; this
// is that logic in one place instead of three. Every page that needs a channel/role picker
// depends on this now.
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

    // Called after creating a channel/role on Discord so the next read reflects it
    // immediately instead of waiting out the 60s cache window.
    public void InvalidateCache(ulong guildId)
    {
        cache.Remove($"discord-guild-channels:{guildId}");
        cache.Remove($"discord-guild-roles:{guildId}");
    }
}
