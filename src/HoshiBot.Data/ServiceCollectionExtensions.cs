using HoshiBot.Data.Seeding;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HoshiBot.Data;

public static class ServiceCollectionExtensions
{
    // "Postgres" (default, used in production/Docker) or "Sqlite" (local dev, zero setup).
    // Registers both IDbContextFactory<HoshiBotDbContext> — a fresh, short-lived context per
    // operation, which every HoshiBot.Web page/service/authorization-handler must use, since a
    // Blazor Server circuit's DI scope spans the whole session (many page navigations), not one
    // request; sharing a single scoped DbContext across that span causes "a second operation was
    // started on this context instance" crashes the moment two components/pages touch it at
    // once — and a scoped HoshiBotDbContext derived from that factory, safe for HoshiBot.Discord's
    // command modules specifically because Discord.NET creates a fresh DI scope per interaction
    // (one command execution = one scope, not one bot-lifetime session). Registering both via
    // AddDbContext + AddDbContextFactory independently causes a DI lifetime-validation error (a
    // singleton factory can't consume the scoped DbContextOptions<T> that AddDbContext registers)
    // — deriving the scoped context from the factory instead avoids a second, conflicting
    // DbContextOptions<T> registration.
    public static IServiceCollection AddHoshiBotDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HoshiBotDbContext");

        services.AddDbContextFactory<HoshiBotDbContext>(options =>
        {
            if (IsSqlite(configuration))
                options.UseSqlite(connectionString ?? "Data Source=hoshibot.dev.db");
            else
                options.UseNpgsql(connectionString);
        });

        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<HoshiBotDbContext>>().CreateDbContext());

        return services;
    }

    // Runs every Seed*IfEmptyAsync that both host processes (HoshiBot.Host and
    // HoshiBot.Web) need on startup, so adding a new seeder only means updating this one
    // method instead of both Program.cs files. SeedGlobalAdminsIfEmptyAsync isn't
    // included — it's web-admin-panel-only, called separately by HoshiBot.Web.
    public static async Task SeedHoshiBotDatabaseAsync(this IServiceProvider services, IConfiguration configuration)
    {
        await services.EnsureHoshiBotDatabaseCreatedIfSqliteAsync(configuration);
        await services.SeedStfcCatalogIfEmptyAsync();
        await services.SeedStfcAlliancesIfEmptyAsync();
        await services.SeedStfcPlayersIfEmptyAsync();
        await services.SeedStfcTerritoriesIfEmptyAsync();
        await services.SeedStfcServerStatusIfEmptyAsync();
        await services.SeedStfcEventStatusIfEmptyAsync();
        await services.SeedGuildSettingsIfEmptyAsync();
    }

    // SQLite dev data is disposable, so it's created directly from the current model
    // instead of via the checked-in Postgres migrations (which stay the schema source
    // of truth, applied in production by HoshiBot.Migrator).
    public static async Task EnsureHoshiBotDatabaseCreatedIfSqliteAsync(this IServiceProvider services, IConfiguration configuration)
    {
        if (!IsSqlite(configuration))
            return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    // Bootstraps the first global admin(s) from config, since nobody could otherwise grant
    // themselves the role via the UI. Only seeds while the table is empty — if every global
    // admin is later removed via the UI, the next restart reseeds from config, giving a
    // built-in recovery path.
    public static async Task SeedGlobalAdminsIfEmptyAsync(this IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();

        if (await db.GlobalAdmins.AnyAsync())
            return;

        var seedIds = configuration.GetSection("GlobalAdmins:DiscordUserIds")
            .GetChildren()
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => ulong.Parse(v!))
            .ToArray();
        if (seedIds.Length == 0)
            return;

        db.GlobalAdmins.AddRange(seedIds.Select(id => new GlobalAdmin { DiscordUserId = id }));
        await db.SaveChangesAsync();
    }

    // Scopely's own region/veil-group numbering — assigned explicitly rather than derived,
    // since it's a fixed set of 3/6 values that will essentially never grow.
    private static readonly Dictionary<string, int> ScopelyRegionIds = new()
    {
        ["US"] = 1,
        ["EU"] = 2,
        ["APAC"] = 3,
    };

    private static readonly Dictionary<string, int> ScopelyVeilGroupIds = new()
    {
        ["US-1"] = 1,
        ["US-2"] = 2,
        ["US-3"] = 3,
        ["EU-4"] = 4,
        ["EU-5"] = 5,
        ["APAC-6"] = 6,
    };

    // Bootstraps the baseline STFC region/veil-group/server catalog from the generated
    // StfcCatalogSeedData (see tools/HoshiBot.StfcCatalogSync). Only seeds while no region
    // exists yet, so it never overwrites catalog data added/edited later via /catalog.
    public static async Task SeedStfcCatalogIfEmptyAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();

        if (await db.StfcRegions.AnyAsync())
            return;

        foreach (var (regionName, number, veilGroupName, serverName, inviteUrl) in StfcCatalogSeedData.Entries)
        {
            var region = await db.StfcRegions.FirstOrDefaultAsync(r => r.Name == regionName);
            if (region is null)
            {
                region = new StfcRegion { Id = ScopelyRegionIds[regionName], Name = regionName };
                db.StfcRegions.Add(region);
                await db.SaveChangesAsync();
            }

            // Newly-launched servers can exist before players are able to fly to a veil
            // group area — real data has some, and they're seeded with no veil group rather
            // than skipped. A server always belongs to a region regardless.
            StfcVeilGroup? veilGroup = null;
            if (veilGroupName is not null)
            {
                veilGroup = await db.StfcVeilGroups.FirstOrDefaultAsync(v => v.Name == veilGroupName);
                if (veilGroup is null)
                {
                    veilGroup = new StfcVeilGroup { Id = ScopelyVeilGroupIds[veilGroupName], Name = veilGroupName, RegionId = region.Id };
                    db.StfcVeilGroups.Add(veilGroup);
                    await db.SaveChangesAsync();
                }
            }

            if (!await db.StfcServers.AnyAsync(s => s.Id == number))
            {
                var server = new StfcServer
                {
                    Id = number,
                    Name = serverName,
                    RegionId = region.Id,
                    VeilGroupId = veilGroup?.Id,
                };
                db.StfcServers.Add(server);

                if (inviteUrl is not null)
                    db.StfcServerDiscordInvites.Add(new StfcServerDiscordInvite { ServerId = server.Id, Url = inviteUrl });

                await db.SaveChangesAsync();
            }
        }
    }

    // Seeds real settings for the user's own guild (see GuildSettingsSeedData for the source
    // — ported from hoshi-bot-yagpdb's definitions-snowflakes.yag), including a placeholder
    // DiscordGuild row if the bot hasn't synced that guild yet (see GuildSyncHandler — this
    // lets the settings be inspected/edited via the web admin without needing a live gateway
    // connection first). Only seeds while this guild has no GuildSettings row yet, so it
    // never overwrites values edited later via /guilds/{id}/settings.
    public static async Task SeedGuildSettingsIfEmptyAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();

        if (await db.GuildSettings.AnyAsync(s => s.GuildId == GuildSettingsSeedData.GuildId))
            return;

        if (!await db.DiscordGuilds.AnyAsync(g => g.Id == GuildSettingsSeedData.GuildId))
        {
            db.DiscordGuilds.Add(new DiscordGuild { Id = GuildSettingsSeedData.GuildId, Name = GuildSettingsSeedData.GuildName });
        }

        db.GuildSettings.Add(GuildSettingsSeedData.CreateSettings());

        db.GuildAlertChannels.AddRange(GuildSettingsSeedData.AlertChannels.Select(c =>
            new GuildAlertChannel { GuildId = GuildSettingsSeedData.GuildId, Kind = c.Kind, ChannelId = c.ChannelId, RoleId = c.RoleId }));

        await db.SaveChangesAsync();
    }

    // Seeds the Territory Capture zone map (names, tiers, neighbours, current
    // weekday/time schedule, and current ownership) from StfcTerritorySeedData. Only
    // seeds while no territory exists yet, so it never overwrites data corrected later.
    // Ownership rows are skipped (not created) for any alliance tag not yet known to
    // StfcAlliances — alliance seeding is a separate concern.
    public static async Task SeedStfcTerritoriesIfEmptyAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();

        if (await db.StfcTerritories.AnyAsync())
            return;

        var territoriesByName = new Dictionary<string, StfcTerritory>();
        foreach (var (id, name, tier, weekday, captureTimeUtc, _) in StfcTerritorySeedData.Entries)
        {
            var territory = new StfcTerritory { Id = id, Name = name, Tier = tier, Weekday = weekday, CaptureTimeUtc = captureTimeUtc };
            db.StfcTerritories.Add(territory);
            territoriesByName[name] = territory;
        }

        await db.SaveChangesAsync();

        foreach (var (_, name, _, _, _, neighbours) in StfcTerritorySeedData.Entries)
        {
            var territory = territoriesByName[name];
            foreach (var neighbourName in neighbours)
            {
                if (!territoriesByName.TryGetValue(neighbourName, out var neighbour))
                    continue;

                db.StfcTerritoryNeighbours.Add(new StfcTerritoryNeighbour
                {
                    TerritoryId = territory.Id,
                    NeighbourTerritoryId = neighbour.Id,
                });
            }
        }

        foreach (var (zoneName, allianceTag) in StfcTerritorySeedData.Ownership)
        {
            var alliance = await db.StfcAlliances.FirstOrDefaultAsync(a => a.Tag == allianceTag);
            if (alliance is null)
                continue;

            db.StfcTerritoryOwnerships.Add(new StfcTerritoryOwnership
            {
                TerritoryId = territoriesByName[zoneName].Id,
                ServerId = alliance.ServerId,
                AllianceId = alliance.Id,
            });
        }

        await db.SaveChangesAsync();
    }

    // Seeds a one-time snapshot of server 164's alliance roster from StfcAllianceSeedData,
    // each with an initial NameHistory row so a future re-sync has a baseline to diff
    // against for rename/re-tag detection. Checked per-server (not table-wide like the
    // other Seed*IfEmptyAsync methods) since this is a partial, single-server snapshot
    // rather than a complete catalog — future per-server seed additions should stay
    // independent of each other. Never overwrites alliances added/edited later via the
    // admin UI. See StfcAllianceSeedData for why this isn't auto-regenerated like
    // StfcCatalogSeedData/StfcTerritorySeedData.
    public static async Task SeedStfcAlliancesIfEmptyAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();

        if (await db.StfcAlliances.AnyAsync(a => a.ServerId == StfcAllianceSeedData.Server164Id))
            return;

        var seededAt = DateTimeOffset.UtcNow;

        foreach (var (externalId, tag, name) in StfcAllianceSeedData.Server164Entries)
        {
            var alliance = new StfcAlliance
            {
                ExternalId = externalId,
                Tag = tag,
                Name = name,
                ServerId = StfcAllianceSeedData.Server164Id,
            };
            alliance.NameHistory.Add(new StfcAllianceNameHistory { Tag = tag, Name = name, ObservedAt = seededAt });

            db.StfcAlliances.Add(alliance);
        }

        await db.SaveChangesAsync();
    }

    // Seeds a one-time snapshot of server 164's player roster from StfcPlayerSeedData,
    // each with an initial NameHistory row so a future re-sync has a baseline to diff
    // against for rename detection. Checked per-server, same reasoning as
    // SeedStfcAlliancesIfEmptyAsync. Must run after alliances are seeded — it resolves
    // AllianceTag against already-seeded StfcAlliances, leaving AllianceId null for
    // unaffiliated players or a tag that doesn't match a seeded alliance.
    public static async Task SeedStfcPlayersIfEmptyAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();

        if (await db.StfcPlayers.AnyAsync(p => p.ServerId == StfcPlayerSeedData.Server164Id))
            return;

        var alliancesByTag = await db.StfcAlliances
            .Where(a => a.ServerId == StfcPlayerSeedData.Server164Id)
            .ToDictionaryAsync(a => a.Tag);

        var seededAt = DateTimeOffset.UtcNow;

        foreach (var (externalId, name, allianceTag) in StfcPlayerSeedData.Server164Entries)
        {
            var alliance = allianceTag is not null ? alliancesByTag.GetValueOrDefault(allianceTag) : null;

            var player = new StfcPlayer
            {
                ExternalId = externalId,
                Name = name,
                ServerId = StfcPlayerSeedData.Server164Id,
                AllianceId = alliance?.Id,
            };
            player.NameHistory.Add(new StfcPlayerNameHistory { Name = name, ObservedAt = seededAt });

            db.StfcPlayers.Add(player);
        }

        await db.SaveChangesAsync();
    }

    // Seeds a one-time snapshot of every known server's up/down/maintenance state from
    // StfcServerStatusSeedData. Must run after SeedStfcCatalogIfEmptyAsync — it FKs into
    // StfcServers. NotifiedStatus/NotifiedMaintenance are set equal to the seeded observed
    // values so ServerStatusNotifyJob doesn't fire a false "changed" notification for
    // every server the first time it runs after this seed.
    public static async Task SeedStfcServerStatusIfEmptyAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();

        if (await db.StfcServerStatuses.AnyAsync())
            return;

        var seededAt = DateTimeOffset.UtcNow;
        var knownServerIds = await db.StfcServers.Select(s => s.Id).ToHashSetAsync();

        db.StfcServerStatuses.AddRange(StfcServerStatusSeedData.Entries
            .Where(e => knownServerIds.Contains(e.StfcServerId))
            .Select(e => new StfcServerStatus
            {
                StfcServerId = e.StfcServerId,
                Status = e.Status,
                Maintenance = e.Maintenance,
                UpdatedAt = seededAt,
                NotifiedStatus = e.Status,
                NotifiedMaintenance = e.Maintenance,
            }));

        await db.SaveChangesAsync();
    }

    // Seeds a one-time snapshot of each recurring event category's most recent occurrence
    // from StfcEventStatusSeedData. NotifiedEventStart is set equal to the seeded
    // EventStart so IncursionNotifyJob doesn't treat the seeded value as a new,
    // not-yet-announced start time.
    public static async Task SeedStfcEventStatusIfEmptyAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();

        if (await db.StfcEventStatuses.AnyAsync())
            return;

        var seededAt = DateTimeOffset.UtcNow;

        db.StfcEventStatuses.AddRange(StfcEventStatusSeedData.Entries.Select(e => new StfcEventStatus
        {
            EventGroup = e.EventGroup,
            EventStart = e.EventStart,
            EventEnd = e.EventEnd,
            Active = e.Active,
            UpdatedAt = seededAt,
            NotifiedEventStart = e.EventStart,
        }));

        await db.SaveChangesAsync();
    }

    private static bool IsSqlite(IConfiguration configuration) =>
        string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
}
