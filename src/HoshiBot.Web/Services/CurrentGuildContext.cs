using System.Security.Claims;
using HoshiBot.Domain.Entities;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace HoshiBot.Web.Services;

// Scoped per-circuit "which guild am I looking at" state for the top-row GuildSelector —
// the fast path for switching guilds without leaving the current page. Every SetAsync
// re-validates against GuildAccessService rather than trusting the stored guild ID, since
// the stored value survives across sessions and admin access can be revoked in between.
public class CurrentGuildContext(GuildAccessService guildAccessService, ProtectedLocalStorage storage)
{
    private const string StorageKey = "hoshibot.currentGuildId";

    public ulong? GuildId { get; private set; }
    public DiscordGuild? Guild { get; private set; }
    public event Action? Changed;

    // Call once, from MainLayout's OnAfterRenderAsync(firstRender) — ProtectedLocalStorage
    // needs an interactive circuit, so this can't run during prerendering.
    public async Task InitializeAsync(ClaimsPrincipal user)
    {
        var result = await storage.GetAsync<ulong>(StorageKey);
        if (result.Success)
            await SetAsync(result.Value, user);
    }

    public async Task<bool> SetAsync(ulong guildId, ClaimsPrincipal user)
    {
        var accessible = await guildAccessService.GetAccessibleGuildsAsync(user);
        var guild = accessible.FirstOrDefault(g => g.Id == guildId);
        if (guild is null)
            return false;

        GuildId = guild.Id;
        Guild = guild;
        await storage.SetAsync(StorageKey, guild.Id);
        Changed?.Invoke();
        return true;
    }

    public async Task ClearAsync()
    {
        GuildId = null;
        Guild = null;
        await storage.DeleteAsync(StorageKey);
        Changed?.Invoke();
    }
}
