using System.Globalization;
using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.TerritoryCapture;

// Core Territory Capture digest logic shared by the weekly and daily Quartz jobs, and by
// TerritoryCaptureRoleSyncJob (which needs the same "this week's owned zones, in slot
// order" computation to assign zone-slot roles).
//
// Partials: this file holds the weekly/daily digests plus the shared slot/ownership queries;
// TerritoryCaptureDigestService.Reminders.cs holds the per-capture reminders (capture-soon,
// services) and the expired-message sweep.
public partial class TerritoryCaptureDigestService(
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

    public Task SendWeeklyDigestsAsync() => SendWeeklyDigestsAsync(DateTimeOffset.UtcNow, onlyGuildId: null);

    // Test/replay seam: run the weekly digest as of an explicit instant and optionally for a single
    // guild / single alliance only. The Quartz sweep passes onlyGuildAllianceId so each alliance fires
    // on its own configured time; the parameterless overload ("now", all guilds/alliances) is unused
    // now but kept as the test/replay entry.
    public async Task SendWeeklyDigestsAsync(DateTimeOffset now, ulong? onlyGuildId, int? onlyGuildAllianceId = null)
    {
        // The weekly digest fires the day before the week begins (Monday, for a Tuesday anchor) and
        // previews the *upcoming* week — GetUpcomingWeekStart, not GetWeekStart (which would snap back
        // to the current, mostly-elapsed week).
        var weekStart = TerritoryCaptureScheduler.GetUpcomingWeekStart(now);

        foreach (var guildId in await GetEligibleGuildIdsAsync())
        {
            if (onlyGuildId is { } only && guildId != only)
                continue;

            var links = await GetTcEnabledLinksAsync(guildId);
            var weekEnd = weekStart.AddDays(6);
            var baseTitle = $"Gebietsübernahmen vom {weekStart.ToDateTime(TimeOnly.MinValue):MMMM d, yyyy} bis {weekEnd.ToDateTime(TimeOnly.MinValue):MMMM d, yyyy}";

            // One digest per TC-enabled alliance, each to its own configured digest channel;
            // the alliance tag is appended to the title only when the guild runs several.
            foreach (var link in links)
            {
                if (onlyGuildAllianceId is { } onlyAlliance && link.Id != onlyAlliance)
                    continue;

                var channelId = await settingsService.GetSnowflakeAsync(
                    guildId, GuildFeature.TerritoryCapture, GuildAudience.Alliance, link.Id, TerritoryCaptureSettingKeys.DigestChannel);
                if (channelId is not { } channelIdValue)
                    continue;

                // One weekly digest per alliance per week — skip if we already posted this week's
                // (idempotent against a Quartz misfire-replay firing the Monday cron twice).
                var dedupKey = $"weekly-{link.Id}-{weekStart:yyyyMMdd}";
                if (await db.TerritoryCaptureSentMessages.AnyAsync(m => m.GuildAllianceId == link.Id && m.DedupKey == dedupKey))
                    continue;

                var (known, unknown) = await GetOwnedZonesAsync(link.StfcAllianceId, weekStart);
                if (known.Count == 0 && unknown.Count == 0)
                    continue;

                // Slot index = position in the week's chronological order — the same numbering the
                // daily digest and the zone-slot roles use, so the row number and button icon match.
                var slotted = known
                    .Select((z, i) => (SlotIndex: i + 1, z.Territory, z.Start, z.End))
                    .ToList();

                // Mention this alliance's absence-clean notification role (owned by the Absences
                // feature; kept in sync by NotificationRoleSyncJob). Weekly pings the whole alliance,
                // unlike the daily which pings only the specific zone-slot roles for tomorrow.
                var notifyRoleId = await settingsService.GetSnowflakeAsync(
                    guildId, GuildFeature.Absences, GuildAudience.Alliance, link.Id, AbsencesSettingKeys.NotificationRole);
                var mentionRoleIds = notifyRoleId is { } roleId
                    ? new List<ulong> { roleId }
                    : new List<ulong>();

                var title = links.Count > 1 ? $"{baseTitle} — [{link.StfcAlliance.Tag}]" : baseTitle;
                var messageId = await SendDigestAsync(guildId, channelIdValue, link, title, slotted, unknown, mentionRoleIds, pin: true);

                // Retention +7 days: no capture-free day anymore, so the pinned digest must live the
                // whole week until the next Monday's digest replaces it. The sweep's delete also drops
                // the stale pin.
                if (messageId is { } weeklyMessageId)
                    await RecordSentMessageAsync(guildId, link.Id, TerritoryCaptureMessageKind.Weekly, dedupKey, channelIdValue, weeklyMessageId, now, now.AddDays(7));
            }
        }
    }

    public Task SendDailyDigestsAsync() => SendDailyDigestsAsync(DateTimeOffset.UtcNow, onlyGuildId: null);

    // Test/replay seam: run the "tomorrow's zones" digest as of an explicit instant (so a full week
    // can be replayed day by day) and optionally for a single guild / single alliance only. The Quartz
    // sweep passes onlyGuildAllianceId so each alliance fires on its own configured time.
    public async Task SendDailyDigestsAsync(DateTimeOffset now, ulong? onlyGuildId, int? onlyGuildAllianceId = null)
    {
        var tomorrow = DateOnly.FromDateTime(now.UtcDateTime).AddDays(1);
        // Base the week on tomorrow, not now: the Monday-night daily is about the new week's first
        // day (Tuesday), which belongs to next week — GetWeekStart(now) would compute the ending
        // week's slots and find nothing for tomorrow, silently skipping the new week's opening day.
        var weekStart = TerritoryCaptureScheduler.GetWeekStart(now.AddDays(1));

        foreach (var guildId in await GetEligibleGuildIdsAsync())
        {
            if (onlyGuildId is { } only && guildId != only)
                continue;

            var links = await GetTcEnabledLinksAsync(guildId);
            foreach (var link in links)
            {
                if (onlyGuildAllianceId is { } onlyAlliance && link.Id != onlyAlliance)
                    continue;

                var channelId = await settingsService.GetSnowflakeAsync(
                    guildId, GuildFeature.TerritoryCapture, GuildAudience.Alliance, link.Id, TerritoryCaptureSettingKeys.DigestChannel);
                if (channelId is not { } channelIdValue)
                    continue;

                // One daily digest per alliance per day (idempotent against a misfire-replay).
                var dedupKey = $"daily-{link.Id}-{tomorrow:yyyyMMdd}";
                if (await db.TerritoryCaptureSentMessages.AnyAsync(m => m.GuildAllianceId == link.Id && m.DedupKey == dedupKey))
                    continue;

                var slots = await GetWeeklySlotAssignmentsAsync(link.StfcAllianceId, weekStart);
                var tomorrowSlots = slots.Where(s => DateOnly.FromDateTime(s.Start.UtcDateTime) == tomorrow).ToList();
                if (tomorrowSlots.Count == 0)
                    continue;

                // Ping the zone-slot role for each of tomorrow's zones, keyed by the same slot index
                // shown on the row and button — so the mention, the row number and the button icon all
                // line up with the weekly preview's numbering.
                var mentionRoleIds = new List<ulong>();
                foreach (var slot in tomorrowSlots)
                {
                    var roleId = await settingsService.GetSnowflakeAsync(
                        guildId, GuildFeature.TerritoryCapture, GuildAudience.Alliance, link.Id,
                        TerritoryCaptureSettingKeys.ZoneSlotRole(slot.SlotIndex));
                    if (roleId is { } rid && !mentionRoleIds.Contains(rid))
                        mentionRoleIds.Add(rid);
                }

                var title = links.Count > 1 ? $"Morgige Gebietsübernahmen — [{link.StfcAlliance.Tag}]" : "Morgige Gebietsübernahmen";
                var known = tomorrowSlots.Select(s => (s.SlotIndex, s.Territory, s.Start, s.End)).ToList();
                var messageId = await SendDigestAsync(guildId, channelIdValue, link, title, known, [], mentionRoleIds, pin: false);

                // Retention +1 day: yesterday's "tomorrow's zones" preview is stale once its day arrives.
                if (messageId is { } dailyMessageId)
                    await RecordSentMessageAsync(guildId, link.Id, TerritoryCaptureMessageKind.Daily, dedupKey, channelIdValue, dailyMessageId, now, now.AddDays(1));
            }
        }
    }

    // Half-hourly sweep (TerritoryCaptureDigestSweepJob) — replaces the two fixed-time crons. Fires each
    // alliance's weekly/daily digest when its configured LOCAL time is due in the alliance's timezone
    // (GuildAlliance.TimeZoneId, DST-aware). Passes onlyGuildAllianceId so each alliance only fires on its
    // own time; the send methods' per-alliance dedup makes each digest post once per week/day.
    public async Task RunDigestSweepAsync(DateTimeOffset now)
    {
        foreach (var guildId in await GetEligibleGuildIdsAsync())
        {
            foreach (var link in await GetTcEnabledLinksAsync(guildId))
            {
                var nowInZone = TimeZoneInfo.ConvertTime(now, GuildAlliance.ResolveTimeZone(link.TimeZoneId));

                var weekly = await GetDigestTimeAsync(guildId, link.Id, TerritoryCaptureSettingKeys.WeeklyDigestTime, TerritoryCaptureSettingKeys.DefaultWeeklyTime);
                if (TerritoryCaptureScheduler.IsWeeklyDigestDue(nowInZone, weekly))
                    await SendWeeklyDigestsAsync(now, guildId, link.Id);

                var daily = await GetDigestTimeAsync(guildId, link.Id, TerritoryCaptureSettingKeys.DailyDigestTime, TerritoryCaptureSettingKeys.DefaultDailyTime);
                if (TerritoryCaptureScheduler.IsDailyDigestDue(nowInZone, daily))
                    await SendDailyDigestsAsync(now, guildId, link.Id);
            }
        }
    }

    private async Task<TimeOnly> GetDigestTimeAsync(ulong guildId, int guildAllianceId, string key, string defaultValue)
    {
        var raw = await settingsService.GetTextAsync(guildId, GuildFeature.TerritoryCapture, GuildAudience.Alliance, guildAllianceId, key);
        return TimeOnly.TryParseExact(raw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : TimeOnly.ParseExact(defaultValue, "HH:mm", CultureInfo.InvariantCulture);
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

    // Returns the posted message id, or null if the send failed (caught RestException). Callers use
    // the id to record a TerritoryCaptureSentMessage so the sweep can clean the message up later.
    private async Task<ulong?> SendDigestAsync(ulong guildId, ulong channelId, GuildAlliance link, string title,
        List<(int SlotIndex, StfcTerritory Territory, DateTimeOffset Start, DateTimeOffset End)> known, List<StfcTerritory> unknown,
        IReadOnlyList<ulong> mentionRoleIds, bool pin)
    {
        // Each row is its OWN inline-code span, with the time appended as real Discord timestamps
        // (<t:unix:t>) OUTSIDE the span. Discord won't render a timestamp inside a code fence, so
        // the legacy design keeps only the aligned columns fenced and lets the time show in each
        // reader's local timezone — the whole reason this isn't one big ``` block.
        var lines = new List<string> { "`#  Zone    Tier  Nachbarn                Tag Zeit`" };
        foreach (var (slotIndex, territory, start, end) in known)
        {
            var neighbours = await GetNeighbourOwnerTagsAsync(territory.Id, link.StfcAlliance.ServerId, link.StfcAlliance.Tag);
            var day = start.ToString("ddd", CultureInfo.GetCultureInfo("de-DE"));
            lines.Add($"`{slotIndex}  {territory.Name,-9}  {territory.Tier}  {string.Join(", ", neighbours),-22}  {day} ` " +
                $"<t:{start.ToUnixTimeSeconds()}:t>-<t:{end.ToUnixTimeSeconds()}:t>");
        }

        if (unknown.Count > 0)
        {
            lines.Add($"Zeit noch unbekannt: {string.Join(", ", unknown.Select(t => t.Name))}");
        }

        var bridgeMention = link.CommandBridgeChannelId is { } bridgeChannelId ? $"<#{bridgeChannelId}>" : "Kommandobrücke";

        var embed = await embedBranding.BuildBrandedAsync(guildId,
            $"Bitte haltet Euch diese Termine nach Möglichkeit frei oder meldet Euch für einzelne Termine hier oder generell auf der {bridgeMention} ab!",
            title: title);
        embed.Fields =
        [
            new EmbedFieldProperties { Name = "Termine", Value = Clamp(string.Join("\n", lines)) },
        ];

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
            .Select(z => new ButtonProperties(
                $"territory-capture-unsubscribe:{z.Territory.Id}:{z.Start.ToUnixTimeSeconds()}:{z.End.ToUnixTimeSeconds()}",
                $"Abmelden für {z.Territory.Name}", EmojiProperties.Standard(DigitEmoji(z.SlotIndex)), ButtonStyle.Primary))
            .ToList();

        var content = mentionRoleIds.Count > 0
            ? string.Join(" ", mentionRoleIds.Select(id => $"<@&{id}>"))
            : null;

        // Discord allows at most 5 buttons per action row and 5 rows per message; chunk so a
        // guild owning more than 5 zones doesn't produce an over-full row (another silent 400).
        var actionRows = buttons
            .Chunk(5)
            .Take(5)
            .Select(chunk => new ActionRowProperties(chunk))
            .ToList();

        RestMessage message;
        try
        {
            message = await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties
            {
                Content = content,
                Embeds = [embed],
                Components = actionRows.Count == 0 ? null : actionRows,
                // Explicitly whitelist the mentioned roles: without this a non-mentionable role
                // renders coloured but never actually pings (the "mention doesn't work" symptom).
                AllowedMentions = mentionRoleIds.Count > 0
                    ? new AllowedMentionsProperties { Everyone = false, AllowedRoles = mentionRoleIds.ToArray() }
                    : AllowedMentionsProperties.None,
            });
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
            return null;
        }

        if (pin)
        {
            try
            {
                await gatewayClient.Rest.PinMessageAsync(channelId, message.Id);
            }
            catch (RestException ex)
            {
                // Pinning is best-effort (missing "Manage Messages", channel already at Discord's
                // 50-pin cap, etc.) and must NOT roll back a successful send: this once lived inside
                // the send's try/catch, so a pin failure returned null here, RecordSentMessageAsync
                // never ran, the dedup row was never written, and the next 30-minute sweep tick
                // re-sent (and re-pinged @role on) the entire digest as a brand-new message.
                logger.LogWarning(ex,
                    "Failed to pin Territory Capture digest message {MessageId} in channel {ChannelId} for guild {GuildId} (alliance {AllianceTag})",
                    message.Id, channelId, guildId, link.StfcAlliance.Tag);
            }
        }

        return message.Id;
    }

    private async Task RecordSentMessageAsync(ulong guildId, int guildAllianceId, TerritoryCaptureMessageKind kind,
        string dedupKey, ulong channelId, ulong messageId, DateTimeOffset sentAt, DateTimeOffset expiresAt)
    {
        db.TerritoryCaptureSentMessages.Add(new TerritoryCaptureSentMessage
        {
            GuildId = guildId,
            GuildAllianceId = guildAllianceId,
            Kind = kind,
            DedupKey = dedupKey,
            ChannelId = channelId,
            MessageId = messageId,
            SentAt = sentAt,
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync();
    }

    // Clamps a value to Discord's embed-field length limit so an unexpectedly long value degrades
    // to a truncated field instead of a hard 400 that fails (and silently hides) the whole send.
    private static string Clamp(string value) =>
        value.Length <= MaxEmbedFieldLength ? value : value[..(MaxEmbedFieldLength - 1)] + "…";

    // Owning-alliance tags of a zone's neighbours, scoped to the alliance's own server. Without
    // the ServerId filter this returned every alliance owning a same-ID territory across ALL ~100
    // game servers (200+ tags, 1000+ chars per zone), overflowing the embed field limit and making
    // every digest send fail with a swallowed 400.
    private async Task<List<string>> GetNeighbourOwnerTagsAsync(int territoryId, int serverId, string ownerTag)
    {
        var neighbourTerritoryIds = await db.StfcTerritoryNeighbours
            .Where(n => n.TerritoryId == territoryId)
            .Select(n => n.NeighbourTerritoryId)
            .ToListAsync();

        if (neighbourTerritoryIds.Count == 0)
            return [];

        // Exclude the owning alliance's own tag — a zone that borders another of its own zones
        // shouldn't list itself as a neighbour.
        return await db.StfcTerritoryOwnerships
            .Where(o => o.ServerId == serverId && neighbourTerritoryIds.Contains(o.TerritoryId) && o.Alliance.Tag != ownerTag)
            .Select(o => o.Alliance.Tag)
            .Distinct()
            .ToListAsync();
    }

    // Keycap-digit emoji for a button icon (1️⃣ … 9️⃣), matching legacy's per-zone digit emoji.
    // Slot indices are 1-5 in practice; the fallback only guards an unexpected value.
    private static string DigitEmoji(int digit) => digit switch
    {
        1 => "1️⃣",
        2 => "2️⃣",
        3 => "3️⃣",
        4 => "4️⃣",
        5 => "5️⃣",
        6 => "6️⃣",
        7 => "7️⃣",
        8 => "8️⃣",
        9 => "9️⃣",
        _ => "🔢",
    };
}
