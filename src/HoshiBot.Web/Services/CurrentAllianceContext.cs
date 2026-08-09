using System.Security.Cryptography;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace HoshiBot.Web.Services;

// Scoped per-circuit "which of the current guild's alliances am I configuring" state, the
// alliance-level companion to CurrentGuildContext. Only meaningful once a guild is selected;
// tracks that guild's linked alliances and the chosen one, so the sidebar's Alliance nav groups
// know what to show, and the feature editors know which alliance's settings to load.
//
// The "chosen one" no longer has a picker of its own — the sidebar lists every alliance as its own
// group, which is a better selector than a dropdown was. It survives as the fallback for the
// alliance-less routes (/manage/guild/{id}/alliance/settings), remembered per guild so returning
// to a coalition guild lands where you left off rather than always on the first alliance.
public class CurrentAllianceContext(
    CurrentGuildContext guildContext,
    GuildAllianceService allianceService,
    ProtectedLocalStorage storage) : IDisposable
{
    private static string StorageKey(ulong guildId) => $"hoshibot.currentAllianceId.{guildId}";

    public IReadOnlyList<GuildAlliance> Alliances { get; private set; } = [];
    public GuildAlliance? Alliance { get; private set; }
    public int? GuildAllianceId => Alliance?.Id;
    public event Action? Changed;

    private bool _subscribed;

    // Call once from MainLayout's OnAfterRenderAsync(firstRender), after the guild context is
    // initialized — ProtectedLocalStorage needs an interactive circuit, like CurrentGuildContext.
    public async Task InitializeAsync()
    {
        if (!_subscribed)
        {
            guildContext.Changed += OnGuildChanged;
            _subscribed = true;
        }

        await ReloadAsync();
    }

    // Re-read the current guild's links and re-resolve the selection: keep the stored/selected
    // alliance if it's still linked, otherwise auto-select (single guild alliance → it; several →
    // the lowest-Id primary). Safe to call whenever links change (add/remove via ScopeEditor).
    public async Task ReloadAsync()
    {
        var guildId = guildContext.GuildId;
        if (guildId is null)
        {
            Alliances = [];
            Alliance = null;
            Changed?.Invoke();
            return;
        }

        Alliances = await allianceService.GetLinksAsync(guildId.Value);
        var storedId = await TryGetStoredAsync(guildId.Value);
        Alliance = Resolve(storedId);
        Changed?.Invoke();
    }

    public async Task<bool> SetAsync(int guildAllianceId)
    {
        var match = Alliances.FirstOrDefault(a => a.Id == guildAllianceId);
        if (match is null)
            return false;

        Alliance = match;
        if (guildContext.GuildId is { } guildId)
            await storage.SetAsync(StorageKey(guildId), guildAllianceId);
        Changed?.Invoke();
        return true;
    }

    private GuildAlliance? Resolve(int? storedId)
    {
        if (Alliances.Count == 0)
            return null;
        if (storedId is { } id && Alliances.FirstOrDefault(a => a.Id == id) is { } stored)
            return stored;
        return Alliances[0]; // GetLinksAsync orders by Id, so this is the primary link.
    }

    private async Task<int?> TryGetStoredAsync(ulong guildId)
    {
        try
        {
            var result = await storage.GetAsync<int>(StorageKey(guildId));
            return result.Success ? result.Value : null;
        }
        catch (CryptographicException)
        {
            // Stored under a Data Protection key no longer in the ring (see CurrentGuildContext
            // for the same handling) — drop it and fall back to auto-select.
            await storage.DeleteAsync(StorageKey(guildId));
            return null;
        }
    }

    private void OnGuildChanged() => _ = ReloadAsync();

    public void Dispose()
    {
        if (_subscribed)
            guildContext.Changed -= OnGuildChanged;
    }
}
