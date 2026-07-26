using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using HoshiBot.Data;
using HoshiBot.Web.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HoshiBot.Web.Services;

// A guild the logged-in user manages on Discord (owner or Manage Server permission),
// whether or not our bot has joined it yet — the only way to see the "not joined yet"
// half is the user's own OAuth guild list, since our own DB obviously has no row for a
// guild the bot was never invited to.
public record UserManagedGuild(ulong Id, string Name, string? IconHash, bool BotInstalled);

// Deliberately has NO dependency on IAuthorizationService (directly or transitively) —
// GuildAdminHandler (an IAuthorizationHandler) depends on this, and authorization handlers
// can never depend on IAuthorizationService: building it needs to enumerate every
// registered handler, so a handler depending on it is a circular dependency by
// construction (confirmed by hitting exactly that at startup before this was split out of
// what's now GuildAccessService, which still needs IAuthorizationService for
// GetAccessibleGuildsAsync and is only used by pages, never by a handler).
public class DiscordUserGuildsService(
    IHttpClientFactory httpClientFactory,
    IDbContextFactory<HoshiBotDbContext> dbFactory,
    IMemoryCache cache,
    ILogger<DiscordUserGuildsService> logger)
{
    private const ulong ManageGuildPermission = 0x20;
    private const string CacheKeyPrefix = "discord-user-guilds:";

    private static readonly JsonSerializerOptions UserGuildsJsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    // Several independent components (MainLayout's CurrentGuildContext init, GuildSelector,
    // Guilds/Index.razor) can all call this for the same user during the same page render.
    // IMemoryCache.GetOrCreateAsync alone doesn't de-duplicate concurrent cache misses —
    // each one would fire its own real HTTP request, which is exactly the kind of burst
    // that trips Discord's notoriously tight rate limit on this specific endpoint. This
    // per-user semaphore ensures only one concurrent caller actually hits the API; the
    // rest await the same result. Never removed (grows one entry per distinct user ever
    // seen) — an accepted trade-off for a hobby-scale, single-instance bot with a small
    // admin population, not worth a cleanup sweep.
    private static readonly ConcurrentDictionary<ulong, SemaphoreSlim> UserLocks = new();

    // The logged-in user's full raw Discord guild list (GET /users/@me/guilds via their
    // own OAuth access token, requires Program.cs's "guilds" scope + SaveTokens = true) —
    // every guild they're a member of, not filtered to admin rights. 60s-cached per user.
    // Returns [] if the access token isn't available (e.g. an expired/stale auth cookie),
    // or if Discord's API itself is unavailable/rate-limited — a failure here must never
    // throw uncaught, since every caller (GuildAdminHandler, GuildAccessService,
    // CurrentGuildContext) sits on a path that can crash the entire Blazor Server circuit
    // (not just one page) if this bubbles up unhandled from MainLayout's first render.
    public async Task<List<DiscordUserGuild>> GetUserDiscordGuildsAsync(string accessToken, ulong userId)
    {
        var cacheKey = CacheKeyPrefix + userId;
        if (cache.TryGetValue(cacheKey, out List<DiscordUserGuild>? cached))
            return cached ?? [];

        var gate = UserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            // Another caller may have populated the cache while we were waiting for the gate.
            if (cache.TryGetValue(cacheKey, out cached))
                return cached ?? [];

            List<DiscordUserGuild>? result;
            try
            {
                var client = httpClientFactory.CreateClient("DiscordUserApi");
                using var request = new HttpRequestMessage(HttpMethod.Get, "users/@me/guilds");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                result = await response.Content.ReadFromJsonAsync<List<DiscordUserGuild>>(UserGuildsJsonOptions);
            }
            catch (HttpRequestException ex)
            {
                // Rate-limited or otherwise unavailable — degrade to "no guilds visible
                // right now" rather than propagating and taking down the caller. Short TTL
                // (not the full 60s) so a transient failure recovers quickly.
                logger.LogWarning(ex, "Discord /users/@me/guilds failed for user {UserId}; degrading to an empty guild list for 5s.", userId);
                cache.Set<List<DiscordUserGuild>?>(cacheKey, null, TimeSpan.FromSeconds(5));
                return [];
            }

            cache.Set(cacheKey, result, TimeSpan.FromSeconds(60));
            return result ?? [];
        }
        finally
        {
            gate.Release();
        }
    }

    // Every Discord guild the user manages (owner or Manage Server permission), whether or
    // not our bot is in it yet — the data source for the guild-picker page. Guilds the bot
    // already has sort first (BotInstalled true, then alphabetical); ones it doesn't sort
    // last, for the "+ Install" affordance.
    public async Task<List<UserManagedGuild>> GetUserManagedGuildsAsync(string accessToken, ulong userId)
    {
        var userGuilds = await GetUserDiscordGuildsAsync(accessToken, userId);
        var managed = userGuilds.Where(g => g.Owner || (g.Permissions & ManageGuildPermission) != 0);

        await using var db = await dbFactory.CreateDbContextAsync();
        var botGuildIds = (await db.DiscordGuilds.Select(g => g.Id).ToListAsync()).ToHashSet();

        return managed
            .Select(g => new UserManagedGuild(g.Id, g.Name, g.IconHash, botGuildIds.Contains(g.Id)))
            .OrderByDescending(g => g.BotInstalled)
            .ThenBy(g => g.Name)
            .ToList();
    }

    // Every guild the user is a member of that our bot is ALSO in — the member-facing
    // counterpart to GetUserManagedGuildsAsync, with no Manage Server filter: this is
    // "communities you and Hoshi share", not "servers you administer". Reuses the same
    // cached /users/@me/guilds call the guild picker and GuildAdminHandler already make,
    // so on a normal session this costs no extra request. BotInstalled is always true (the
    // list is intersected with our DiscordGuild rows), so there's no "not added yet" half.
    public async Task<List<UserManagedGuild>> GetUserSharedBotGuildsAsync(string accessToken, ulong userId)
    {
        var userGuilds = await GetUserDiscordGuildsAsync(accessToken, userId);

        await using var db = await dbFactory.CreateDbContextAsync();
        var botGuildIds = (await db.DiscordGuilds.Select(g => g.Id).ToListAsync()).ToHashSet();

        return userGuilds
            .Where(g => botGuildIds.Contains(g.Id))
            .Select(g => new UserManagedGuild(g.Id, g.Name, g.IconHash, true))
            .OrderBy(g => g.Name)
            .ToList();
    }

    // Every guild the bot is in (all our DiscordGuild rows), shaped for the guild picker —
    // the support-mode data source, where a global admin sees all guilds regardless of
    // their own Discord membership/permissions. All BotInstalled = true (a DiscordGuild row
    // only exists for a guild the bot has joined), so there's no "not added yet" half.
    // IconHash comes from the DiscordGuild row (kept current by GuildSyncHandler on gateway
    // connect); GuildIcon falls back to name initials when it's still null.
    public async Task<List<UserManagedGuild>> GetAllBotGuildsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.DiscordGuilds
            .OrderBy(g => g.Name)
            .Select(g => new UserManagedGuild(g.Id, g.Name, g.IconHash, true))
            .ToListAsync();
    }
}
