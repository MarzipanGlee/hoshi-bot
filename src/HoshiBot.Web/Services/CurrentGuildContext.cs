using System.Security.Claims;
using System.Security.Cryptography;
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
        try
        {
            var result = await storage.GetAsync<ulong>(StorageKey);
            if (result.Success)
                await SetAsync(result.Value, user);
        }
        catch (CryptographicException)
        {
            // The stored value was protected with a Data Protection key that's no longer in the
            // ring (e.g. the gitignored dev key folder was regenerated across the WSL2 move, or
            // keys rotated). Unprotecting it throws here — treat it as absent and delete it so it
            // stops crashing the circuit on every page load instead of just re-selecting a guild.
            await storage.DeleteAsync(StorageKey);
        }
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
