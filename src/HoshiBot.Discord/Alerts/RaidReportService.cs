using System.Globalization;
using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.Alerts;

// The weekly raid report: one post per alliance, every Monday, summarising the raids reported during
// the week that just ended. Ported from hoshi-bot-yagpdb's raid-report.yag — the last piece of the
// raid feature that had not been carried over, which is why RaidAlertsSettingKeys.ReportChannel
// existed with nothing reading it.
//
// The post is deliberately the same shape as legacy's: a per-commander list of raids, the covered
// period, the two counts, and a closing nudge toward the shield reminder on the Command Bridge. The
// no-raids variant is a congratulation rather than an empty report, which is the half of it members
// actually look forward to.
public class RaidReportService(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    EmbedBranding embedBranding,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    GuildAllianceService allianceService,
    GuildMemberNames memberNames,
    LanguageResolver languageResolver,
    ILogger<RaidReportService> logger)
{
    public Task SendDueReportsAsync(CancellationToken cancellationToken = default) =>
        SendDueReportsAsync(DateTimeOffset.UtcNow, cancellationToken);

    // Swept hourly. Each alliance is checked against its own local fire time, so a guild spanning
    // time zones posts each report at 09:00 where that alliance lives rather than all at once.
    public async Task SendDueReportsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        foreach (var guildId in await featureService.GetEnabledGuildIdsAsync(GuildFeature.RaidAlerts, cancellationToken))
        {
            var enabledAllianceIds = await featureService.GetEnabledAllianceIdsAsync(guildId, GuildFeature.RaidAlerts);

            foreach (var allianceId in enabledAllianceIds)
            {
                var alliance = await allianceService.FindByIdAsync(guildId, allianceId);
                if (alliance is null)
                    continue;

                var zone = GuildAlliance.ResolveTimeZone(alliance.TimeZoneId);
                var nowInZone = TimeZoneInfo.ConvertTime(now, zone);

                var localTime = await GetReportTimeAsync(guildId, allianceId);
                if (!RaidReportScheduler.IsDue(nowInZone, localTime))
                    continue;

                var (weekStart, weekEnd) = RaidReportScheduler.GetReportWeek(nowInZone);

                // One report per alliance per week. Comparing the covered week rather than a
                // timestamp means a bot that was down all Monday still posts on Tuesday, once.
                var weekKey = $"{weekStart:yyyy}-W{RaidReportScheduler.WeekNumber(weekStart):00}";
                var lastWeek = await settingsService.GetTextAsync(
                    guildId, GuildFeature.RaidAlerts, GuildAudience.Alliance, allianceId, RaidAlertsSettingKeys.ReportLastWeek);
                if (lastWeek == weekKey)
                    continue;

                try
                {
                    await SendReportAsync(guildId, alliance, weekStart, weekEnd, zone, cancellationToken);
                }
                catch (Exception ex) when (ex is RestException or InvalidOperationException)
                {
                    // A failed report must not block next week's, but it must not be marked sent
                    // either — leaving the marker alone lets the next sweep retry it.
                    logger.LogWarning(ex, "Could not post the weekly raid report for alliance {AllianceId} in guild {GuildId}", allianceId, guildId);
                    continue;
                }

                await settingsService.SetTextAsync(
                    guildId, GuildFeature.RaidAlerts, GuildAudience.Alliance, allianceId, RaidAlertsSettingKeys.ReportLastWeek, weekKey);
            }
        }
    }

    private async Task SendReportAsync(ulong guildId, GuildAlliance alliance, DateOnly weekStart, DateOnly weekEnd,
        TimeZoneInfo zone, CancellationToken cancellationToken)
    {
        var channelId = await settingsService.GetSnowflakeAsync(
            guildId, GuildFeature.RaidAlerts, GuildAudience.Alliance, alliance.Id, RaidAlertsSettingKeys.ReportChannel);
        if (channelId is not { } reportChannelId)
            return;

        // The window is the alliance's local week, converted to absolute instants for the query —
        // a raid at 23:30 local on Sunday belongs to this report, not next week's.
        var start = new DateTimeOffset(weekStart.ToDateTime(TimeOnly.MinValue), zone.GetUtcOffset(weekStart.ToDateTime(TimeOnly.MinValue)));
        var endExclusive = new DateTimeOffset(weekEnd.AddDays(1).ToDateTime(TimeOnly.MinValue), zone.GetUtcOffset(weekEnd.AddDays(1).ToDateTime(TimeOnly.MinValue)));

        var lang = await languageResolver.ForAllianceAsync(alliance.Id);

        // Terminated raids only, and only real ones: a test alert is a rehearsal, and a raid that
        // ended within minutes is a misclick. Both were excluded by legacy for the same reasons.
        var raids = await db.Alerts
            .Include(a => a.StfcSystem)
            .Where(a => a.GuildId == guildId
                && a.Type == AlertType.Raid
                && !a.IsTest
                && a.TerminatedAt != null
                && a.TriggeredAt >= start
                && a.TriggeredAt < endExclusive)
            .OrderBy(a => a.TriggeredAt)
            .ToListAsync(cancellationToken);

        var reportable = raids
            .Where(a => a.TerminatedAt!.Value - a.TriggeredAt > RaidReportScheduler.MinimumReportableRaid)
            .ToList();

        // Group by the raided commander, and keep only those belonging to THIS alliance — a
        // coalition guild's alliances each get their own report. Resolved from the member's linked
        // player now rather than stored on the alert, so someone who changed alliance mid-week is
        // counted where they are today; the alternative is a column that would need backfilling.
        var byCommander = new Dictionary<ulong, List<Alert>>();
        foreach (var raid in reportable)
        {
            var raidAlliance = await allianceService.FindByMemberAsync(guildId, raid.TargetDiscordUserId)
                ?? (await allianceService.GetPrimaryIdAsync(guildId) is { } primaryId ? await allianceService.FindByIdAsync(guildId, primaryId) : null);
            if (raidAlliance?.Id != alliance.Id)
                continue;

            if (!byCommander.TryGetValue(raid.TargetDiscordUserId, out var list))
                byCommander[raid.TargetDiscordUserId] = list = [];
            list.Add(raid);
        }

        var embed = await BuildEmbedAsync(guildId, alliance, weekStart, weekEnd, zone, byCommander, lang);

        // The alliance's notification role, the same one the capture digest and elevated
        // announcements ping — this is alliance-wide news, not something members opted into
        // separately.
        var content = alliance.NotificationRoleId is { } roleId ? $"<@&{roleId}>" : null;

        await gatewayClient.Rest.SendMessageAsync(reportChannelId, new MessageProperties
        {
            Content = content,
            Embeds = [embed],
        }, cancellationToken: cancellationToken);
    }

    private async Task<EmbedProperties> BuildEmbedAsync(ulong guildId, GuildAlliance alliance,
        DateOnly weekStart, DateOnly weekEnd, TimeZoneInfo zone,
        Dictionary<ulong, List<Alert>> byCommander, Language lang)
    {
        var raidCount = byCommander.Sum(c => c.Value.Count);

        string description;
        if (raidCount == 0)
        {
            description = Msg.Raid.ReportNone(lang);
        }
        else
        {
            var sections = new List<string>();
            foreach (var (userId, raids) in byCommander.OrderBy(c => c.Value[0].TriggeredAt))
            {
                var commander = await memberNames.ResolveNameAsync(guildId, userId);
                var lines = raids.Select(r => FormatRaid(r, lang));
                sections.Add($"{commander}:\n{string.Join('\n', lines)}");
            }

            description = $"{Msg.Raid.ReportIntro(lang)}\n\n{string.Join("\n\n", sections)}";
        }

        var embed = await embedBranding.BuildBrandedAsync(guildId, description,
            title: Msg.Raid.ReportTitle(lang, RaidReportScheduler.WeekNumber(weekStart)));

        // Local midnight-to-midnight, rendered as Discord timestamps so every reader sees the window
        // in their own zone — the same <t:…:F> pair legacy used.
        var periodStart = new DateTimeOffset(weekStart.ToDateTime(TimeOnly.MinValue), zone.GetUtcOffset(weekStart.ToDateTime(TimeOnly.MinValue)));
        var periodEnd = new DateTimeOffset(weekEnd.ToDateTime(new TimeOnly(23, 59)), zone.GetUtcOffset(weekEnd.ToDateTime(new TimeOnly(23, 59))));

        var fields = new List<EmbedFieldProperties>
        {
            new EmbedFieldProperties()
                .WithName(Msg.Raid.ReportPeriod(lang))
                .WithValue(Msg.Raid.ReportPeriodValue(lang,
                    $"<t:{periodStart.ToUnixTimeSeconds()}:F>", $"<t:{periodEnd.ToUnixTimeSeconds()}:F>")),
        };

        if (raidCount > 0)
        {
            fields.Add(new EmbedFieldProperties()
                .WithName(Msg.Raid.ReportCountRaids(lang))
                .WithValue(raidCount.ToString(CultureInfo.InvariantCulture))
                .WithInline());
            fields.Add(new EmbedFieldProperties()
                .WithName(Msg.Raid.ReportCountCommanders(lang))
                .WithValue(byCommander.Count.ToString(CultureInfo.InvariantCulture))
                .WithInline());
        }

        // The nudge points at the user Command Bridge, where the shield reminder button lives. An
        // alliance without one gets no hint rather than a broken channel mention.
        if (alliance.CommandBridgeChannelId is { } bridgeChannelId)
        {
            var mention = $"<#{bridgeChannelId}>";
            fields.Add(new EmbedFieldProperties()
                .WithName(Msg.Raid.ReportShieldHint(lang))
                .WithValue(raidCount > 0
                    ? Msg.Raid.ReportShieldHintRaids(lang, mention)
                    : Msg.Raid.ReportShieldHintNone(lang, mention)));
        }

        embed.Fields = fields;
        return embed;
    }

    private static string FormatRaid(Alert raid, Language lang)
    {
        var when = $"<t:{raid.TriggeredAt.ToUnixTimeSeconds()}:F>";
        var duration = RaidReportScheduler.FormatDuration(raid.TerminatedAt!.Value - raid.TriggeredAt);
        var system = raid.StfcSystem?.Name ?? raid.Detail ?? "?";

        return string.IsNullOrWhiteSpace(raid.Attacker)
            ? Msg.Raid.ReportEntry(lang, when, duration, system)
            : Msg.Raid.ReportEntryAttacker(lang, when, duration, system, raid.Attacker);
    }

    private async Task<TimeOnly> GetReportTimeAsync(ulong guildId, int guildAllianceId)
    {
        var raw = await settingsService.GetTextAsync(
            guildId, GuildFeature.RaidAlerts, GuildAudience.Alliance, guildAllianceId, RaidAlertsSettingKeys.ReportTime);

        return TimeOnly.TryParseExact(raw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : TimeOnly.ParseExact(RaidReportScheduler.DefaultLocalTime, "HH:mm", CultureInfo.InvariantCulture);
    }
}
