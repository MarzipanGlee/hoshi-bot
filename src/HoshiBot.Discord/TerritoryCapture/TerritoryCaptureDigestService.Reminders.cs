using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;

namespace HoshiBot.Discord.TerritoryCapture;

// The per-capture reminder side: the pre-capture "capture soon" reminder (with unsubscribe
// button), the post-capture "activate services" (Dienste) officer nudge, and the retention sweep
// that deletes every tracked TC message once it expires. The weekly/daily digests and the shared
// slot/ownership queries live in TerritoryCaptureDigestService.cs.
public partial class TerritoryCaptureDigestService
{
    // How far before a capture the single "capture soon" reminder fires (legacy's 35-minute window).
    // With the job running every few minutes, the first tick inside the window sends it ~30 min out;
    // the relative <t:…:R> timestamp shows the true remaining time regardless.
    private static readonly TimeSpan ReminderLeadTime = TimeSpan.FromMinutes(35);

    // How long after a capture ends the "activate services" (Dienste) reminder fires (legacy's
    // ~5-min-after). With the job running every 5 min, a 10-min catch window guarantees exactly one
    // tick lands inside [End, End + this] — the same reasoning as ReminderLeadTime, just after the end.
    private static readonly TimeSpan ServicesLeadTime = TimeSpan.FromMinutes(10);

    // How long a posted Services reminder stays before the sweep removes it — long enough for the
    // day's officers to see it, short enough not to accumulate.
    private static readonly TimeSpan ServicesRetention = TimeSpan.FromHours(6);

    public Task SendCaptureRemindersAsync() => SendCaptureRemindersAsync(DateTimeOffset.UtcNow);

    // Posts a single "capture soon" reminder for each owned zone whose window is about to open, then
    // sweeps away any TC message (single/daily/weekly) whose retention has elapsed. Runs every few
    // minutes; the per-capture dedup key + the unique index keep a zone reminded at most once.
    public async Task SendCaptureRemindersAsync(DateTimeOffset now)
    {
        foreach (var guildId in await GetEligibleGuildIdsAsync())
        {
            var weekStart = TerritoryCaptureScheduler.GetWeekStart(now);
            foreach (var link in await GetTcEnabledLinksAsync(guildId))
            {
                // Both reminder kinds are this alliance's own public posts — its language.
                // Sign-off doesn't come into it: neither reminder carries a button (the daily and
                // weekly digests do).
                var lang = await languageResolver.ForAllianceAsync(link.Id);

                var channelId = await settingsService.GetSnowflakeAsync(
                    guildId, GuildFeature.TerritoryCapture, GuildAudience.Alliance, link.Id, TerritoryCaptureSettingKeys.DigestChannel);
                if (channelId is not { } channelIdValue)
                    continue;

                foreach (var slot in await GetWeeklySlotAssignmentsAsync(link.StfcAllianceId, weekStart))
                {
                    // Fire once in the window [Start - lead, Start); a capture already under way
                    // (now >= Start) is skipped — the button is only useful before it begins.
                    var untilStart = slot.Start - now;
                    if (untilStart <= TimeSpan.Zero || untilStart > ReminderLeadTime)
                        continue;

                    var dedupKey = $"single-{link.Id}-{slot.Territory.Id}-{slot.Start.ToUnixTimeSeconds()}";
                    if (await db.TerritoryCaptureSentMessages.AnyAsync(m => m.GuildAllianceId == link.Id && m.DedupKey == dedupKey))
                        continue;

                    var messageId = await SendCaptureReminderAsync(guildId, channelIdValue, link, slot, lang);
                    if (messageId is { } mid)
                        await RecordSentMessageAsync(guildId, link.Id, TerritoryCaptureMessageKind.Single, dedupKey, channelIdValue, mid, now, slot.End);
                }

                // Services (Dienste) reminder: a post-capture nudge for officers to activate the
                // zone's services, posted to its own channel ~5 min after each capture ends. Its own
                // feature — an alliance can want the capture digests without it (and vice versa) —
                // so it is gated separately here rather than riding along with Territory Capture.
                if (!await featureService.IsEnabledAsync(guildId, GuildFeature.TerritoryCaptureServiceReminders, GuildAudience.Alliance, link.Id))
                    continue;

                var servicesChannelId = await settingsService.GetSnowflakeAsync(
                    guildId, GuildFeature.TerritoryCaptureServiceReminders, GuildAudience.Alliance, link.Id,
                    TerritoryCaptureServiceRemindersSettingKeys.ServicesChannel);
                if (servicesChannelId is not { } servicesChannelIdValue)
                    continue;

                foreach (var slot in await GetWeeklySlotAssignmentsAsync(link.StfcAllianceId, weekStart))
                {
                    // Fire once in the window [End, End + lead); the capture must already have ended.
                    var sinceEnd = now - slot.End;
                    if (sinceEnd < TimeSpan.Zero || sinceEnd > ServicesLeadTime)
                        continue;

                    var dedupKey = $"services-{link.Id}-{slot.Territory.Id}-{slot.End.ToUnixTimeSeconds()}";
                    if (await db.TerritoryCaptureSentMessages.AnyAsync(m => m.GuildAllianceId == link.Id && m.DedupKey == dedupKey))
                        continue;

                    var messageId = await SendServicesReminderAsync(guildId, servicesChannelIdValue, link, slot, lang);
                    if (messageId is { } mid)
                        await RecordSentMessageAsync(guildId, link.Id, TerritoryCaptureMessageKind.Services, dedupKey, servicesChannelIdValue, mid, now, slot.End + ServicesRetention);
                }
            }
        }

        await SweepExpiredMessagesAsync(now);
    }

    // The 30-minute pre-capture reminder, rebuilt to match the legacy bot's exactly (see
    // hoshi-bot-yagpdb/Commands/notifications/send-territory-capture-reminder.yag, ReminderType.Single):
    // the tier note, then the same aligned #/Zone/Tier/Neighbours/Day/Time table the weekly digest
    // uses, and the live relative start in the title.
    //
    // No sign-off button, deliberately — legacy attaches buttons only when the reminder is NOT the
    // single-zone one (`if and $buttons (not …Single)`). Signing off still lives on the daily/weekly
    // digest and the Command Bridge; a button 30 minutes before the window is too late to be the
    // point anyway.
    private async Task<ulong?> SendCaptureReminderAsync(ulong guildId, ulong channelId, GuildAlliance link,
        (int SlotIndex, StfcTerritory Territory, DateTimeOffset Start, DateTimeOffset End) slot, Language lang)
    {
        var startUnix = slot.Start.ToUnixTimeSeconds();
        var endUnix = slot.End.ToUnixTimeSeconds();

        var roleId = await settingsService.GetSnowflakeAsync(
            guildId, GuildFeature.TerritoryCapture, GuildAudience.Alliance, link.Id, TerritoryCaptureSettingKeys.ZoneSlotRole(slot.SlotIndex));

        // Same construction as SendDigestAsync's schedule: the columns stay inside their own inline
        // code span and the times sit outside it, because Discord won't render <t:…> inside a code
        // fence and each reader should see their own timezone.
        var neighbours = await GetNeighbourOwnerTagsAsync(slot.Territory.Id, link.StfcAlliance.ServerId, link.StfcAlliance.Tag);
        var day = slot.Start.ToString("ddd", Languages.ToCulture(lang));
        var row = Msg.Tc.ScheduleRow(lang, slot.SlotIndex, $"{slot.Territory.Name,-9}", slot.Territory.Tier,
            $"{string.Join(", ", neighbours),-22}", day, startUnix, endUnix);

        // Which alliances may contest this zone at all — a tier fact, and the one piece of context
        // that tells the reader whether to expect a fight. Empty for tiers legacy never described.
        var tierNote = Msg.Tc.TierDescription(lang, slot.Territory.Tier);
        var body = string.IsNullOrEmpty(tierNote)
            ? $"{Msg.Tc.ScheduleHeader(lang)}\n{row}"
            : $"{tierNote}\n\n{Msg.Tc.ScheduleHeader(lang)}\n{row}";

        var embed = await embedBranding.BuildBrandedAsync(guildId, body,
            title: Msg.Tc.ReminderTitle(lang, slot.Territory.Name, startUnix));

        try
        {
            var message = await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties
            {
                Content = roleId is { } rid ? $"<@&{rid}>" : null,
                Embeds = [embed],
                AllowedMentions = roleId is { } r
                    ? new AllowedMentionsProperties { Everyone = false, AllowedRoles = new[] { r } }
                    : AllowedMentionsProperties.None,
            });
            return message.Id;
        }
        catch (RestException ex)
        {
            logger.LogWarning(ex,
                "Failed to send Territory Capture reminder to channel {ChannelId} for guild {GuildId} (alliance {AllianceTag}, zone {Zone})",
                channelId, guildId, link.StfcAlliance.Tag, slot.Territory.Name);
            return null;
        }
    }

    // The post-capture "activate services" (Dienste) reminder for officers — a plain branded embed
    // pinging the alliance's configured services role. No unsubscribe/ack button (unlike the
    // pre-capture reminder); it's an after-the-fact officer nudge.
    private async Task<ulong?> SendServicesReminderAsync(ulong guildId, ulong channelId, GuildAlliance link,
        (int SlotIndex, StfcTerritory Territory, DateTimeOffset Start, DateTimeOffset End) slot, Language lang)
    {
        var roleId = await settingsService.GetSnowflakeAsync(
            guildId, GuildFeature.TerritoryCaptureServiceReminders, GuildAudience.Alliance, link.Id,
            TerritoryCaptureServiceRemindersSettingKeys.ServicesRole);

        var description = await BuildServicesDescriptionAsync(link, slot.Territory, lang);

        var embed = await embedBranding.BuildBrandedAsync(guildId, description,
            title: Msg.Tc.ServicesTitle(lang, slot.Territory.Name));

        try
        {
            var message = await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties
            {
                Content = roleId is { } rid ? $"<@&{rid}>" : null,
                Embeds = [embed],
                AllowedMentions = roleId is { } r
                    ? new AllowedMentionsProperties { Everyone = false, AllowedRoles = new[] { r } }
                    : AllowedMentionsProperties.None,
            });
            return message.Id;
        }
        catch (RestException ex)
        {
            logger.LogWarning(ex,
                "Failed to send Territory Capture services reminder to channel {ChannelId} for guild {GuildId} (alliance {AllianceTag}, zone {Zone})",
                channelId, guildId, link.StfcAlliance.Tag, slot.Territory.Name);
            return null;
        }
    }

    // Builds the Services reminder body for a zone. If the alliance has curated a Service Selection
    // for this zone, renders two ordered groups (obligatorisch / optional). Otherwise falls back to
    // the full list of the zone's services (all in canonical slot order), or a generic nudge when
    // even that is empty (server not synced). Service names stay English game terms; the framing
    // renders in the alliance's language.
    private async Task<string> BuildServicesDescriptionAsync(GuildAlliance link, StfcTerritory territory, Language lang)
    {
        var slots = await db.StfcTerritoryServiceSlots
            .Where(s => s.ServerId == link.StfcAlliance.ServerId && s.TerritoryId == territory.Id)
            .OrderBy(s => s.Position)
            .Select(s => new { s.ServiceId, s.Service.Name })
            .ToListAsync();

        if (slots.Count == 0)
            return Msg.Tc.ServicesGenericNudge(lang, territory.Name);

        var priorityByService = await db.TerritoryServiceSelections
            .Where(x => x.GuildAllianceId == link.Id && x.TerritoryId == territory.Id)
            .ToDictionaryAsync(x => x.ServiceId, x => x.Priority);

        List<string> InGroup(TerritoryServicePriority priority) => slots
            .Where(s => priorityByService.TryGetValue(s.ServiceId, out var p) && p == priority)
            .Select(s => s.Name)
            .ToList();

        var mustHave = priorityByService.Count > 0 ? InGroup(TerritoryServicePriority.MustHave) : [];
        var niceToHave = priorityByService.Count > 0 ? InGroup(TerritoryServicePriority.NiceToHave) : [];

        // No curation (or every curated service has since dropped off the zone) → list all, game order.
        if (mustHave.Count == 0 && niceToHave.Count == 0)
            return Clamp(Msg.Tc.ServicesAllInOrder(lang, territory.Name, Numbered(slots.Select(s => s.Name))));

        var parts = new List<string> { Msg.Tc.ServicesEnded(lang, territory.Name) };
        if (mustHave.Count > 0)
            parts.Add(Msg.Tc.ServicesMustHave(lang, Numbered(mustHave)));
        if (niceToHave.Count > 0)
            parts.Add(Msg.Tc.ServicesNiceToHave(lang, Numbered(niceToHave)));

        return Clamp(string.Join("\n\n", parts));
    }

    private static string Numbered(IEnumerable<string> names) =>
        string.Join("\n", names.Select((name, i) => $"{i + 1}. {name}"));

    // Deletes any tracked TC message past its retention (Single at capture End, Daily +1d, Weekly
    // +7d, Services at End +6h) and drops its row. Deleting a pinned weekly also removes its pin. Not feature-gated:
    // cleanup must run even if Territory Capture was disabled after the message was posted.
    private async Task SweepExpiredMessagesAsync(DateTimeOffset now)
    {
        var expired = await db.TerritoryCaptureSentMessages
            .Where(m => m.ExpiresAt < now)
            .ToListAsync();

        foreach (var sent in expired)
        {
            try
            {
                await gatewayClient.Rest.DeleteMessageAsync(sent.ChannelId, sent.MessageId);
            }
            catch (RestException ex)
            {
                // Already gone / channel unreachable — drop the row anyway so we don't retry forever.
                logger.LogDebug(ex,
                    "Failed to delete expired Territory Capture message {MessageId} in channel {ChannelId}",
                    sent.MessageId, sent.ChannelId);
            }

            db.TerritoryCaptureSentMessages.Remove(sent);
        }

        if (expired.Count > 0)
            await db.SaveChangesAsync();
    }
}
