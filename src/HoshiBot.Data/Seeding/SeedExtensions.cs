using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HoshiBot.Data.Seeding;

// Startup seeding for both host processes. Split out of ServiceCollectionExtensions so DI
// registration and seed logic live apart; each seeder opens with WithDbAsync and guards on
// its table being empty, so nothing here ever overwrites data edited later via bot or web.
public static class SeedExtensions
{
    // Scoped-DbContext boilerplate shared by every seeder below: open a DI scope, resolve a
    // HoshiBotDbContext, run the seeder body, dispose. Each seeder just supplies its own
    // empty-guard + AddRange body.
    private static async Task WithDbAsync(this IServiceProvider services, Func<HoshiBotDbContext, Task> action)
    {
        using var scope = services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>());
    }

    // Runs every Seed*IfEmptyAsync that both host processes (HoshiBot.Host and
    // HoshiBot.Web) need on startup, so adding a new seeder only means updating this one
    // method instead of both Program.cs files. SeedGlobalAdminsIfEmptyAsync isn't
    // included — it's web-admin-panel-only, called separately by HoshiBot.Web.
    public static async Task SeedHoshiBotDatabaseAsync(this IServiceProvider services)
    {
        await services.SeedStfcCatalogIfEmptyAsync();
        await services.SeedStfcAlliancesIfEmptyAsync();
        await services.SeedStfcPlayersIfEmptyAsync();
        await services.SeedStfcTerritoriesIfEmptyAsync();
        await services.SeedStfcTerritoryOwnershipIfEmptyAsync();
        await services.SeedStfcServerStatusIfEmptyAsync();
        await services.SeedStfcEventStatusIfEmptyAsync();
        await services.SeedIncursionsRegionDefaultsIfEmptyAsync();
        await services.SeedStfcNewsSettingsIfEmptyAsync();
        await services.SeedGuildSettingsIfEmptyAsync();
        await services.SeedHoshiTestGuildSettingsIfEmptyAsync();

        // Self-heal: attach any Alliance-audience feature rows still left unscoped (seeded or
        // migrated before their guild linked an alliance) to each guild's primary link. Safe to
        // run every startup — it only touches rows with a null GuildAllianceId.
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<GuildAllianceService>().AdoptAllOrphansAsync();
    }

    // Bootstraps the first global admin(s) from config, since nobody could otherwise grant
    // themselves the role via the UI. Only seeds while the table is empty — if every global
    // admin is later removed via the UI, the next restart reseeds from config, giving a
    // built-in recovery path.
    public static Task SeedGlobalAdminsIfEmptyAsync(this IServiceProvider services, IConfiguration configuration) =>
        services.WithDbAsync(async db =>
        {
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
        });

    // Bootstraps the baseline STFC region/veil-group/server catalog from the generated
    // StfcCatalogSeedData (see tools/HoshiBot.StfcSeedSync). Only seeds while no region
    // exists yet, so it never overwrites catalog data added/edited later via /catalog or the
    // admin panel's Import page (StfcCatalogImportService), which is the ongoing-refresh path
    // once this initial seed has run.
    public static Task SeedStfcCatalogIfEmptyAsync(this IServiceProvider services) =>
        services.WithDbAsync(async db =>
        {
            if (await db.StfcRegions.AnyAsync())
                return;

            foreach (var (regionName, number, veilGroupName, serverName, inviteUrl) in StfcCatalogSeedData.Entries)
            {
                var region = await db.StfcRegions.FirstOrDefaultAsync(r => r.Name == regionName);
                if (region is null)
                {
                    region = new StfcRegion { Id = ScopelyCatalogIds.Regions[regionName], Name = regionName };
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
                        veilGroup = new StfcVeilGroup { Id = ScopelyCatalogIds.VeilGroups[veilGroupName], Name = veilGroupName, RegionId = region.Id };
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
        });

    // Seeds real settings for the user's own guild (see GuildSettingsSeedData for the source
    // — ported from hoshi-bot-yagpdb's definitions-snowflakes.yag), including a placeholder
    // DiscordGuild row if the bot hasn't synced that guild yet (see GuildSyncHandler — this
    // lets the settings be inspected/edited via the web admin without needing a live gateway
    // connection first). Only seeds while this guild has no GuildSettings row yet, so it
    // never overwrites values edited later via /guilds/{id}/settings.
    public static Task SeedGuildSettingsIfEmptyAsync(this IServiceProvider services) =>
        services.WithDbAsync(async db =>
        {
            if (await db.GuildSettings.AnyAsync(s => s.GuildId == GuildSettingsSeedData.GuildId))
                return;

            if (!await db.DiscordGuilds.AnyAsync(g => g.Id == GuildSettingsSeedData.GuildId))
            {
                db.DiscordGuilds.Add(new DiscordGuild { Id = GuildSettingsSeedData.GuildId, Name = GuildSettingsSeedData.GuildName });
            }

            db.GuildSettings.Add(GuildSettingsSeedData.CreateSettings());

            // This guild's own linked alliance. All the per-feature settings below are seeded under
            // GuildAudience.Alliance, which is now scoped per specific linked alliance — so they hang
            // off this link (via the navigation property, so a single SaveChanges assigns its Id and
            // fills the FK). Requires StfcAlliance 7433 to be seeded already (SeedStfcAlliancesIfEmptyAsync
            // runs earlier in SeedHoshiBotDatabaseAsync).
            var ownAlliance = GuildSettingsSeedData.CreateOwnAlliance();
            db.GuildAlliances.Add(ownAlliance);

            // Raid/Shield alert channels are an Alliance-only feature — the only audience this
            // seeded guild uses (see GuildSettingsSeedData's doc comment). GuildAlertChannel is not
            // per-alliance (stays audience-scoped), so no link reference here.
            db.GuildAlertChannels.AddRange(GuildSettingsSeedData.AlertChannels.Select(c =>
                new GuildAlertChannel
                {
                    GuildId = GuildSettingsSeedData.GuildId,
                    Kind = c.Kind,
                    ChannelId = c.ChannelId,
                    RoleId = c.RoleId,
                    Audience = GuildAudience.Alliance,
                }));

            db.GuildFeatureSettingSnowflakes.AddRange(GuildSettingsSeedData.SnowflakeSettings.Select(s =>
                new GuildFeatureSettingSnowflake
                {
                    GuildId = GuildSettingsSeedData.GuildId,
                    Feature = s.Feature,
                    Audience = GuildAudience.Alliance,
                    GuildAlliance = ownAlliance,
                    Key = s.Key,
                    Value = s.Value,
                }));

            db.GuildEnabledFeatures.AddRange(GuildSettingsSeedData.EnabledFeatures.Select(feature =>
            {
                var audience = GuildFeatureAudiences.HasMultipleAudiences(feature) ? GuildAudience.Alliance : GuildFeatureAudiences.RelevantAudiences(feature);
                return new GuildEnabledFeature
                {
                    GuildId = GuildSettingsSeedData.GuildId,
                    Feature = feature,
                    Audience = audience,
                    // Every seeded feature resolves to the Alliance audience for this guild, so each
                    // scopes to the own-alliance link.
                    GuildAlliance = audience == GuildAudience.Alliance ? ownAlliance : null,
                };
            }));

            await db.SaveChangesAsync();
        });

    // The bot's own dev/test Discord guild — not a real alliance/production guild, just
    // somewhere to point channel-creation flows (SetupWizard, AlertChannelListEditor's
    // "create new channel", etc.) at while testing against a live gateway connection. Only
    // seeds while this guild has no GuildSettings row yet, so it never overwrites a value set
    // later via the admin UI — same convention as SeedGuildSettingsIfEmptyAsync, just far more
    // minimal (no alert channels/feature settings, since this guild isn't meant to exercise
    // those). The default channel category is now per-alliance (GuildAlliance) — set it via the
    // Alliance Settings page once this guild links an alliance.
    private const ulong HoshiTestGuildId = 1296179770421149786;
    private const string HoshiTestGuildName = "Hoshi Bot";

    public static Task SeedHoshiTestGuildSettingsIfEmptyAsync(this IServiceProvider services) =>
        services.WithDbAsync(async db =>
        {
            if (await db.GuildSettings.AnyAsync(s => s.GuildId == HoshiTestGuildId))
                return;

            if (!await db.DiscordGuilds.AnyAsync(g => g.Id == HoshiTestGuildId))
            {
                db.DiscordGuilds.Add(new DiscordGuild { Id = HoshiTestGuildId, Name = HoshiTestGuildName });
            }

            db.GuildSettings.Add(new GuildSettings
            {
                GuildId = HoshiTestGuildId,
            });

            await db.SaveChangesAsync();
        });

    // Seeds the Territory Capture zone map (names, tiers, neighbours, current weekday/time
    // schedule) from StfcTerritorySeedData. Only seeds while no territory exists yet, so it
    // never overwrites data corrected later. Ownership is seeded separately
    // (SeedStfcTerritoryOwnershipIfEmptyAsync) under its own guard, since it depends on
    // alliances already being seeded.
    public static Task SeedStfcTerritoriesIfEmptyAsync(this IServiceProvider services) =>
        services.WithDbAsync(async db =>
        {
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

            await db.SaveChangesAsync();
        });

    // Seeds per-server territory ownership from StfcTerritoryOwnershipSeedData (a snapshot of
    // stfc.pro's stfc_territories feed). Guarded on ownership being empty (not on territories
    // being empty) so it self-heals: if territories were seeded in an earlier state where the
    // owning alliances weren't resolvable yet, ownership stays empty and this backfills it on
    // the next startup once the alliances exist. Must run after both the territory and alliance
    // seeders. Each (Server, Tag) is resolved against StfcAlliances (a Tag alone is not globally
    // unique, so resolution is scoped per server); rows with a null Tag, or a Tag/Territory not
    // present in the DB, are skipped.
    public static Task SeedStfcTerritoryOwnershipIfEmptyAsync(this IServiceProvider services) =>
        services.WithDbAsync(async db =>
        {
            if (await db.StfcTerritoryOwnerships.AnyAsync())
                return;

            var territoryIds = (await db.StfcTerritories.Select(t => t.Id).ToListAsync()).ToHashSet();
            if (territoryIds.Count == 0)
                return;

            // (ServerId, Tag) -> AllianceId, resolved in-memory so the thousands of ownership rows
            // don't each cost a DB round-trip. Tag is unique per server, so the key is unambiguous.
            var alliances = await db.StfcAlliances.Select(a => new { a.Id, a.ServerId, a.Tag }).ToListAsync();
            var allianceIdByServerTag = alliances
                .GroupBy(a => (a.ServerId, a.Tag))
                .ToDictionary(g => g.Key, g => g.First().Id);

            foreach (var (server, territory, tag) in StfcTerritoryOwnershipSeedData.Entries)
            {
                if (tag is null || !territoryIds.Contains(territory))
                    continue;

                if (!allianceIdByServerTag.TryGetValue((server, tag), out var allianceId))
                    continue;

                db.StfcTerritoryOwnerships.Add(new StfcTerritoryOwnership
                {
                    TerritoryId = territory,
                    ServerId = server,
                    AllianceId = allianceId,
                });
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // HoshiBot.Host and HoshiBot.Web both seed on startup; if they race an empty table the
                // loser hits the (TerritoryId, ServerId) unique index and its whole insert rolls back.
                // That's benign — the winner already seeded the full snapshot — so treat it as done
                // rather than crashing startup. (This race used to double every row before the index.)
            }
        });

    // Seeds a one-time snapshot of every server's alliance roster from StfcAllianceSeedData,
    // each with an initial NameHistory row so a future re-sync has a baseline to diff
    // against for rename/re-tag detection. Table-wide (unlike the old per-server check)
    // since this snapshot already covers every known server, not just one. Must run after
    // SeedStfcCatalogIfEmptyAsync — it FKs into StfcServers. Never overwrites alliances
    // added/edited later via the admin UI. See StfcAllianceSeedData for why this isn't
    // auto-regenerated like StfcCatalogSeedData/StfcTerritorySeedData.
    public static Task SeedStfcAlliancesIfEmptyAsync(this IServiceProvider services) =>
        services.WithDbAsync(async db =>
        {
            if (await db.StfcAlliances.AnyAsync())
                return;

            var seededAt = DateTimeOffset.UtcNow;

            // The snapshot is a faithful dump of every alliance the source knew about, which can
            // include a handful on brand-new servers not yet in our curated StfcServers catalog
            // (they show up in alliance data before they're catalogued — see the STFC data doc).
            // Skip those rather than FK-crash the whole seed; they'll come in with the next catalog
            // + alliance refresh.
            var knownServerIds = (await db.StfcServers.Select(s => s.Id).ToListAsync()).ToHashSet();

            foreach (var (externalId, serverId, tag, name, emblem) in StfcAllianceSeedData.Entries)
            {
                if (!knownServerIds.Contains(serverId))
                    continue;

                var alliance = new StfcAlliance
                {
                    ExternalId = externalId,
                    Tag = tag,
                    Name = name,
                    ServerId = serverId,
                    Emblem = emblem,
                };
                alliance.NameHistory.Add(new StfcAllianceNameHistory { Tag = tag, Name = name, ObservedAt = seededAt });

                db.StfcAlliances.Add(alliance);
            }

            await db.SaveChangesAsync();
        });

    // Seeds a one-time snapshot of server 164's player roster from StfcPlayerSeedData,
    // each with an initial NameHistory row so a future re-sync has a baseline to diff
    // against for rename detection. Checked per-server, same reasoning as
    // SeedStfcAlliancesIfEmptyAsync. Must run after alliances are seeded — it resolves
    // AllianceTag against already-seeded StfcAlliances, leaving AllianceId null for
    // unaffiliated players or a tag that doesn't match a seeded alliance.
    public static Task SeedStfcPlayersIfEmptyAsync(this IServiceProvider services) =>
        services.WithDbAsync(async db =>
        {
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
        });

    // Seeds a one-time snapshot of every known server's up/down/maintenance state from
    // StfcServerStatusSeedData. Must run after SeedStfcCatalogIfEmptyAsync — it FKs into
    // StfcServers. NotifiedStatus/NotifiedMaintenance are set equal to the seeded observed
    // values so ServerStatusNotifyJob doesn't fire a false "changed" notification for
    // every server the first time it runs after this seed.
    public static Task SeedStfcServerStatusIfEmptyAsync(this IServiceProvider services) =>
        services.WithDbAsync(async db =>
        {
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
        });

    // Seeds a one-time snapshot of each recurring event category's most recent occurrence
    // from StfcEventStatusSeedData. NotifiedEventStart is set equal to the seeded
    // EventStart so InfiniteIncursionsNotifyJob doesn't treat the seeded value as a new,
    // not-yet-announced start time.
    public static Task SeedStfcEventStatusIfEmptyAsync(this IServiceProvider services) =>
        services.WithDbAsync(async db =>
        {
            if (await db.StfcEventStatuses.AnyAsync())
                return;

            var seededAt = DateTimeOffset.UtcNow;

            db.StfcEventStatuses.AddRange(StfcEventStatusSeedData.Entries.Select(e => new StfcEventStatus
            {
                EventGroup = e.EventGroup,
                RegionId = e.RegionId,
                EventStart = e.EventStart,
                EventEnd = e.EventEnd,
                Active = e.Active,
                UpdatedAt = seededAt,
                NotifiedEventStart = e.EventStart,
            }));

            await db.SaveChangesAsync();
        });

    // Seeds the preserved daily "time of day" Infinite Incursions starts in each region (US
    // 15:00 UTC, EU 08:00 UTC, APAC 23:00 UTC — the same real values StfcEventStatusSeedData
    // uses), so an admin-submitted date can be combined with these to produce each region's
    // EventStart without re-entering the time every event. Must run after
    // SeedStfcCatalogIfEmptyAsync — it FKs into StfcRegions. Only seeds while empty, so it
    // never overwrites values corrected later via the admin UI.
    public static Task SeedIncursionsRegionDefaultsIfEmptyAsync(this IServiceProvider services) =>
        services.WithDbAsync(async db =>
        {
            if (await db.IncursionsRegionDefaults.AnyAsync())
                return;

            var defaultsByRegionId = new Dictionary<int, TimeOnly>
            {
                [ScopelyCatalogIds.Regions["US"]] = new TimeOnly(15, 0),
                [ScopelyCatalogIds.Regions["EU"]] = new TimeOnly(8, 0),
                [ScopelyCatalogIds.Regions["APAC"]] = new TimeOnly(23, 0),
            };

            var knownRegionIds = await db.StfcRegions.Select(r => r.Id).ToHashSetAsync();

            db.IncursionsRegionDefaults.AddRange(defaultsByRegionId
                .Where(kv => knownRegionIds.Contains(kv.Key))
                .Select(kv => new IncursionsRegionDefault { RegionId = kv.Key, DefaultStartTimeUtc = kv.Value }));

            await db.SaveChangesAsync();
        });

    // Seeds the single StfcNewsSettings row (Id = 1) with its default values. Only seeds
    // while the table is empty, so it never overwrites values edited later via the admin UI.
    public static Task SeedStfcNewsSettingsIfEmptyAsync(this IServiceProvider services) =>
        services.WithDbAsync(async db =>
        {
            if (await db.StfcNewsSettings.AnyAsync())
                return;

            db.StfcNewsSettings.Add(new StfcNewsSettings { Id = 1 });
            await db.SaveChangesAsync();
        });
}
