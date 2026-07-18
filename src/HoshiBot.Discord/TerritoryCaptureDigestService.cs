using System.Globalization;
using System.Text;
using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord;

// Core Territory Capture digest logic shared by the weekly and daily Quartz jobs, and by
// TerritoryCaptureRoleSyncJob (which needs the same "this week's owned zones, in slot
// order" computation to assign zone-slot roles).
public class TerritoryCaptureDigestService(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    EmbedBranding embedBranding,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    ILogger<TerritoryCaptureDigestService> logger)
{
    // Discord's hard limit on a single embed field's Value; exceeding it makes the whole
    // SendMessageAsync throw a 400 RestException (hit for real — the neighbour-tag list once
    // blew past this and every digest silently failed, see GetNeighbourOwnerTagsAsync).
    private const int MaxEmbedFieldLength = 1024;

    public async Task SendWeeklyDigestsAsync()
    {
        var weekStart = TerritoryCaptureScheduler.GetWeekStart(DateTimeOffset.UtcNow);

        foreach (var guildId in await GetEligibleGuildIdsAsync())
        {
            var links = await GetTcEnabledLinksAsync(guildId);
            var weekEnd = weekStart.AddDays(6);
            var baseTitle = $"Gebietsübernahmen vom {weekStart.ToDateTime(TimeOnly.MinValue):MMMM d, yyyy} bis {weekEnd.ToDateTime(TimeOnly.MinValue):MMMM d, yyyy}";

            var notificationRole = await db.NotificationRoles
                .FirstOrDefaultAsync(r => r.GuildId == guildId && r.Kind == NotificationRoleKind.General);

            // One digest per TC-enabled alliance, each to its own configured digest channel;
            // the alliance tag is appended to the title only when the guild runs several.
            foreach (var link in links)
            {
                var channelId = await settingsService.GetSnowflakeAsync(
                    guildId, GuildFeature.TerritoryCapture, GuildAudience.Alliance, link.Id, TerritoryCaptureSettingKeys.DigestChannel);
                if (channelId is not { } channelIdValue)
                    continue;

                var (known, unknown) = await GetOwnedZonesAsync(link.StfcAllianceId, weekStart);
                if (known.Count == 0 && unknown.Count == 0)
                    continue;

                var title = links.Count > 1 ? $"{baseTitle} — [{link.StfcAlliance.Tag}]" : baseTitle;
                await SendDigestAsync(guildId, channelIdValue, link, title, known, unknown, notificationRole?.DiscordRoleId, pin: true);
            }
        }
    }

    public async Task SendDailyDigestsAsync()
    {
        var tomorrow = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime).AddDays(1);
        var weekStart = TerritoryCaptureScheduler.GetWeekStart(DateTimeOffset.UtcNow);

        foreach (var guildId in await GetEligibleGuildIdsAsync())
        {
            var links = await GetTcEnabledLinksAsync(guildId);
            foreach (var link in links)
            {
                var channelId = await settingsService.GetSnowflakeAsync(
                    guildId, GuildFeature.TerritoryCapture, GuildAudience.Alliance, link.Id, TerritoryCaptureSettingKeys.DigestChannel);
                if (channelId is not { } channelIdValue)
                    continue;

                var slots = await GetWeeklySlotAssignmentsAsync(link.StfcAllianceId, weekStart);
                var tomorrowSlots = slots.Where(s => DateOnly.FromDateTime(s.Start.UtcDateTime) == tomorrow).ToList();
                if (tomorrowSlots.Count == 0)
                    continue;

                var mentionRoleId = await settingsService.GetSnowflakeAsync(
                    guildId, GuildFeature.TerritoryCapture, GuildAudience.Alliance, link.Id,
                    TerritoryCaptureSettingKeys.ZoneSlotRole(tomorrowSlots[0].SlotIndex));

                var title = links.Count > 1 ? $"Morgige Gebietsübernahmen — [{link.StfcAlliance.Tag}]" : "Morgige Gebietsübernahmen";
                var known = tomorrowSlots.Select(s => (s.Territory, s.Start, s.End)).ToList();
                await SendDigestAsync(guildId, channelIdValue, link, title, known, [], mentionRoleId, pin: false);
            }
        }
    }

    // The guild's linked alliances that have Territory Capture enabled, ordered by link id
    // (slot roles/settings are keyed per alliance). Shared with TerritoryCaptureRoleSyncJob.
    public async Task<List<GuildAlliance>> GetTcEnabledLinksAsync(ulong guildId)
    {
        var enabledIds = await featureService.GetEnabledAllianceIdsAsync(guildId, GuildFeature.TerritoryCapture);
        if (enabledIds.Count == 0)
            return [];

        return await db.GuildAlliances
            .Include(ga => ga.StfcAlliance)
            .Where(ga => ga.GuildId == guildId && enabledIds.Contains(ga.Id))
            .OrderBy(ga => ga.Id)
            .ToListAsync();
    }

    // This week's zones owned by one alliance, in slot order (slot 1 = earliest window that
    // week). Shared with TerritoryCaptureRoleSyncJob so both use the exact same per-alliance
    // ordering when assigning that alliance's 5 fixed zone-slot roles.
    public async Task<List<(int SlotIndex, StfcTerritory Territory, DateTimeOffset Start, DateTimeOffset End)>> GetWeeklySlotAssignmentsAsync(
        int stfcAllianceId, DateOnly weekStart)
    {
        var (known, _) = await GetOwnedZonesAsync(stfcAllianceId, weekStart);
        return known
            .Select((z, index) => (SlotIndex: index + 1, z.Territory, z.Start, z.End))
            .ToList();
    }

    private async Task<List<ulong>> GetEligibleGuildIdsAsync()
    {
        var guildIds = await db.GuildAlliances.Select(ga => ga.GuildId).Distinct().ToListAsync();
        var disabled = new List<ulong>();
        foreach (var guildId in guildIds)
        {
            if (!await featureService.IsEnabledAsync(guildId, GuildFeature.TerritoryCapture))
                disabled.Add(guildId);
        }

        return guildIds.Except(disabled).ToList();
    }

    private async Task<(List<(StfcTerritory Territory, DateTimeOffset Start, DateTimeOffset End)> Known, List<StfcTerritory> Unknown)> GetOwnedZonesAsync(
        int stfcAllianceId, DateOnly weekStart)
    {
        // DistinctBy guards against duplicate ownership rows for the same territory (the table
        // has historically held exact-duplicate rows from concurrent seeding); without it a zone
        // would be listed — and get a duplicate unsubscribe button — once per duplicate row.
        var territories = (await db.StfcTerritoryOwnerships
            .Where(o => o.AllianceId == stfcAllianceId)
            .Select(o => o.Territory)
            .ToListAsync())
            .DistinctBy(t => t.Id)
            .ToList();

        var known = new List<(StfcTerritory, DateTimeOffset, DateTimeOffset)>();
        var unknown = new List<StfcTerritory>();

        foreach (var territory in territories)
        {
            var window = TerritoryCaptureScheduler.GetCaptureWindow(territory, weekStart);
            if (window is null)
                unknown.Add(territory);
            else
                known.Add((territory, window.Value.Start, window.Value.End));
        }

        return (known.OrderBy(z => z.Item2).ToList(), unknown);
    }

    private async Task SendDigestAsync(ulong guildId, ulong channelId, GuildAlliance link, string title,
        List<(StfcTerritory Territory, DateTimeOffset Start, DateTimeOffset End)> known, List<StfcTerritory> unknown,
        ulong? mentionRoleId, bool pin)
    {
        var table = new StringBuilder();
        table.AppendLine("```");
        table.AppendLine($"{"#",-3}{"Zone",-12}{"Tier",-5}{"Nachbarn",-24}{"Tag",-5}{"Zeit",-16}");

        var index = 0;
        foreach (var (territory, start, end) in known)
        {
            index++;
            var neighbours = await GetNeighbourOwnerTagsAsync(territory.Id, link.StfcAlliance.ServerId);
            var day = start.ToString("ddd", CultureInfo.GetCultureInfo("de-DE"));
            var time = $"{start:HH:mm}-{end:HH:mm}";
            table.AppendLine($"{index,-3}{territory.Name,-12}{territory.Tier,-5}{string.Join(", ", neighbours),-24}{day,-5}{time,-16}");
        }

        table.AppendLine("```");

        if (unknown.Count > 0)
        {
            table.AppendLine($"Zeit noch unbekannt: {string.Join(", ", unknown.Select(t => t.Name))}");
        }

        var embed = new EmbedProperties
        {
            Title = title,
            Description = "Bitte haltet Euch diese Termine nach Möglichkeit frei oder meldet Euch für einzelne Termine hier oder generell auf der #kommandobrücke ab!",
            Fields =
            [
                new EmbedFieldProperties { Name = "Termine", Value = Clamp(table.ToString()) },
            ],
            Color = EmbedBranding.BotColor,
            Author = await embedBranding.BuildAuthorAsync(guildId),
            Footer = embedBranding.BuildFooter(guildId),
        };

        var instructions = await settingsService.GetTextAsync(
            guildId, GuildFeature.TerritoryCapture, GuildAudience.Alliance, link.Id, TerritoryCaptureSettingKeys.Instructions);
        if (!string.IsNullOrWhiteSpace(instructions))
        {
            embed.Fields = embed.Fields.Append(new EmbedFieldProperties
            {
                Name = $"Anweisungen von {link.StfcAlliance.Tag}-Führungsstab",
                Value = Clamp(instructions),
            });
        }

        var buttons = known
            .Select((z, i) => new ButtonProperties(
                $"territory-capture-unsubscribe:{z.Territory.Id}:{z.Start.ToUnixTimeSeconds()}:{z.End.ToUnixTimeSeconds()}",
                $"{i + 1} Abmelden für {z.Territory.Name}", ButtonStyle.Primary))
            .ToList();

        var content = mentionRoleId is { } roleId ? $"<@&{roleId}>" : null;

        // Discord allows at most 5 buttons per action row and 5 rows per message; chunk so a
        // guild owning more than 5 zones doesn't produce an over-full row (another silent 400).
        var actionRows = buttons
            .Chunk(5)
            .Take(5)
            .Select(chunk => new ActionRowProperties(chunk))
            .ToList();

        try
        {
            var message = await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties
            {
                Content = content,
                Embeds = [embed],
                Components = actionRows.Count == 0 ? null : actionRows,
            });

            if (pin)
                await gatewayClient.Rest.PinMessageAsync(channelId, message.Id);
        }
        catch (RestException ex)
        {
            // Channel unreachable (missing permissions, deleted, etc.) — skip this guild rather
            // than throwing and blocking every other guild's digest. Logged (not swallowed
            // silently) so a genuine failure is visible: a bare catch here once hid a 400 from an
            // oversized embed field for weeks, so no digest was ever posted and nothing showed up
            // in the logs.
            logger.LogWarning(ex,
                "Failed to send Territory Capture digest to channel {ChannelId} for guild {GuildId} (alliance {AllianceTag})",
                channelId, guildId, link.StfcAlliance.Tag);
        }
    }

    // Clamps a value to Discord's embed-field length limit so an unexpectedly long value degrades
    // to a truncated field instead of a hard 400 that fails (and silently hides) the whole send.
    private static string Clamp(string value) =>
        value.Length <= MaxEmbedFieldLength ? value : value[..(MaxEmbedFieldLength - 1)] + "…";

    // Owning-alliance tags of a zone's neighbours, scoped to the alliance's own server. Without
    // the ServerId filter this returned every alliance owning a same-ID territory across ALL ~100
    // game servers (200+ tags, 1000+ chars per zone), overflowing the embed field limit and making
    // every digest send fail with a swallowed 400.
    private async Task<List<string>> GetNeighbourOwnerTagsAsync(int territoryId, int serverId)
    {
        var neighbourTerritoryIds = await db.StfcTerritoryNeighbours
            .Where(n => n.TerritoryId == territoryId)
            .Select(n => n.NeighbourTerritoryId)
            .ToListAsync();

        if (neighbourTerritoryIds.Count == 0)
            return [];

        return await db.StfcTerritoryOwnerships
            .Where(o => o.ServerId == serverId && neighbourTerritoryIds.Contains(o.TerritoryId))
            .Select(o => o.Alliance.Tag)
            .Distinct()
            .ToListAsync();
    }
}
