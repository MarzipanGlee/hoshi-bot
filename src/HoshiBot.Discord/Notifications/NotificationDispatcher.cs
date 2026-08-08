using System.Collections.Concurrent;
using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.Notifications;

// Shared public/DM notification fan-out for Alerts and Shield Reminders (and, per the
// original plan, reusable as-is by later phases like territory capture/announcements).
public class NotificationDispatcher(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    ILogger<NotificationDispatcher> logger,
    EmbedBranding embedBranding,
    GuildFeatureService featureService,
    ChannelCooldown cooldown,
    LanguageResolver languageResolver)
{
    // Plain-string overload for callers whose content is language-independent or already
    // rendered; the factory overload below is the localized path.
    public Task<List<(ulong ChannelId, ulong? MessageId)>> SendPublicAsync(ulong guildId, GuildAlertChannelKind kind, string content,
        ButtonProperties? terminateButton = null, EmbedProperties? embed = null) =>
        SendPublicAsync(guildId, kind, _ => content,
            terminateButton is null ? null : _ => new ActionRowProperties([terminateButton]),
            embed is null ? null : _ => Task.FromResult<EmbedProperties?>(embed));

    // Localized fan-out: each GuildAlertChannel row renders in its own audience's language
    // (factories are memoized per distinct language, so a single-language guild builds once).
    public async Task<List<(ulong ChannelId, ulong? MessageId)>> SendPublicAsync(ulong guildId, GuildAlertChannelKind kind,
        Func<Language, string> content, Func<Language, ActionRowProperties?>? buttons = null,
        Func<Language, Task<EmbedProperties?>>? embed = null)
    {
        var channels = await db.GuildAlertChannels
            .Where(c => c.GuildId == guildId && c.Kind == kind)
            .ToListAsync();

        return await SendToChannelsAsync(guildId, channels, content, buttons, embed);
    }

    // Same as SendPublicAsync, but gates per-row instead of once per guild: only sends to
    // channels whose own tagged Audience is enabled for feature. Used by
    // ServerStatus/InfiniteIncursions, whose GuildAlertChannel rows can span multiple audiences for
    // the same guild — disabling the feature for one audience must not silence channels
    // tagged for a different audience the guild also serves.
    public Task<List<(ulong ChannelId, ulong? MessageId)>> SendPublicToEnabledAudiencesAsync(
        ulong guildId, GuildAlertChannelKind kind, GuildFeature feature, string content,
        ButtonProperties? terminateButton = null, EmbedProperties? embed = null) =>
        SendPublicToEnabledAudiencesAsync(guildId, kind, feature, _ => content,
            terminateButton is null ? null : _ => new ActionRowProperties([terminateButton]),
            embed is null ? null : _ => Task.FromResult<EmbedProperties?>(embed));

    public async Task<List<(ulong ChannelId, ulong? MessageId)>> SendPublicToEnabledAudiencesAsync(
        ulong guildId, GuildAlertChannelKind kind, GuildFeature feature, Func<Language, string> content,
        Func<Language, ActionRowProperties?>? buttons = null, Func<Language, Task<EmbedProperties?>>? embed = null)
    {
        var channels = await db.GuildAlertChannels
            .Where(c => c.GuildId == guildId && c.Kind == kind)
            .ToListAsync();

        var enabledAudiences = await featureService.GetEnabledAudiencesAsync(guildId, feature);
        var eligible = channels.Where(c => enabledAudiences.Contains(c.Audience)).ToList();

        return await SendToChannelsAsync(guildId, eligible, content, buttons, embed);
    }

    private async Task<List<(ulong ChannelId, ulong? MessageId)>> SendToChannelsAsync(
        ulong guildId, List<GuildAlertChannel> channels, Func<Language, string> content,
        Func<Language, ActionRowProperties?>? buttons, Func<Language, Task<EmbedProperties?>>? embed)
    {
        var results = new List<(ulong, ulong?)>();
        var rendered = new Dictionary<Language, (string Content, ActionRowProperties? Buttons, EmbedProperties? Embed)>();

        foreach (var channel in channels)
        {
            var lang = await ResolveChannelLanguageAsync(guildId, channel.Audience);
            if (!rendered.TryGetValue(lang, out var r))
                rendered[lang] = r = (content(lang), buttons?.Invoke(lang), embed is null ? null : await embed(lang));

            results.Add((channel.ChannelId, await TrySendToChannelAsync(guildId, channel.ChannelId,
                $"<@&{channel.RoleId}> {r.Content}", r.Buttons, r.Embed, "alert")));
        }

        return results;
    }

    // GuildAlertChannel rows are audience-tagged but not alliance-tagged, so an
    // Alliance-audience row can't resolve a specific alliance's language — it gets the
    // guild language instead (documented edge in docs/localization-plan.md). Guild/None
    // rows are guild-scoped by definition.
    private Task<Language> ResolveChannelLanguageAsync(ulong guildId, GuildAudience audience) =>
        audience is GuildAudience.Alliance or GuildAudience.Guild or GuildAudience.None
            ? languageResolver.ForGuildAsync(guildId)
            : languageResolver.ForAudienceAsync(guildId, audience);

    // One channel send plus the shared undeliverable handling (warn log, activity-log entry,
    // throttled admin ping) used by both SendToChannelsAsync and SendToChannelIdsAsync — the two
    // only differ in how they build the content prefix and what channelKind the log line names.
    // Returns the sent message's id, or null when the channel was skipped (Forbidden/NotFound).
    private async Task<ulong?> TrySendToChannelAsync(ulong guildId, ulong channelId, string content,
        ActionRowProperties? buttons, EmbedProperties? embed, string channelKind)
    {
        // Alert fan-out runs on 15-second and 1-minute triggers, so an undeliverable channel was
        // the highest-frequency repeat in the app after the Command Bridge queue.
        if (cooldown.IsCoolingDown(channelId, BotAction.SendAlert))
            return null;

        try
        {
            var message = await gatewayClient.Rest.SendMessageAsync(channelId,
                new MessageProperties
                {
                    Content = content,
                    Embeds = embed is null ? null : [embed],
                    Components = buttons is null ? null : [buttons],
                });
            cooldown.RecordSuccess(channelId, BotAction.SendAlert);
            return message.Id;
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            cooldown.RecordFailure(channelId, BotAction.SendAlert);
            logger.LogWarning("Skipped {ChannelKind} channel {ChannelId} for guild {GuildId}: {StatusCode}",
                channelKind, channelId, guildId, ex.StatusCode);

            // One report, not two. There used to be a separate "a message could not be sent to
            // #x" activity-log line alongside this; now that the permission report goes to the same
            // log channel and names the actual missing permission, that line was a strictly worse
            // duplicate of it — and a third REST call on a path that had just failed twice.
            await NotifyAdminOfPermissionIssueAsync(guildId, BotAction.SendAlert, channelId, ChannelAccessProfile.Post.Permissions());
            return null;
        }
    }

    // Posts to an explicit set of channel ids, mentioning mentionRoleId only when set — the
    // channel-list counterpart to SendPublicAsync (which reads GuildAlertChannel rows and pings
    // each row's own role). Used by ClientRelease, whose channels live in GuildFeatureChannel and
    // whose role is per-platform, not per-channel.
    public async Task<List<(ulong ChannelId, ulong? MessageId)>> SendToChannelIdsAsync(
        ulong guildId, IReadOnlyList<ulong> channelIds, ulong? mentionRoleId, string content, EmbedProperties? embed = null)
    {
        var results = new List<(ulong, ulong?)>();
        var prefix = mentionRoleId is { } roleId ? $"<@&{roleId}> " : "";

        foreach (var channelId in channelIds)
            results.Add((channelId, await TrySendToChannelAsync(guildId, channelId,
                $"{prefix}{content}", buttons: null, embed, "feature")));

        return results;
    }

    // Sends to guildId's configured AdminChannelId (GuildSettings) and returns the sent
    // message's (channelId, messageId) so a caller can persist it for later edits — unlike
    // NotifyAdminOfPermissionIssueAsync (hardcoded wording, 1h throttle keyed by a free-text
    // context string), this is generic and untimed: callers own their own dedup (e.g. a real
    // DB row), not a time window. Returns null if AdminChannelId isn't configured or the send
    // fails, so callers can just skip rather than branch on a missing setting.
    public async Task<(ulong ChannelId, ulong MessageId)?> SendToAdminChannelAsync(
        ulong guildId, string? content = null, EmbedProperties? embed = null, IReadOnlyList<ButtonProperties>? buttons = null)
    {
        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings?.AdminChannelId is not { } channelId)
            return null;

        // StfcNewsNotifyJob's catch-up re-tries every unresolved post × every guild with no message
        // row on it, and that set only grows — so an unwritable admin channel repeated forever and
        // got steadily worse. Callers already treat null as "didn't send".
        if (cooldown.IsCoolingDown(channelId, BotAction.SendAlert))
            return null;

        try
        {
            var message = await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties
            {
                Content = content,
                Embeds = embed is null ? null : [embed],
                Components = buttons is null ? null : [new ActionRowProperties(buttons)],
            });
            cooldown.RecordSuccess(channelId, BotAction.SendAlert);
            return (channelId, message.Id);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            cooldown.RecordFailure(channelId, BotAction.SendAlert);
            logger.LogWarning("Could not send admin channel notification to {ChannelId} for guild {GuildId}: {StatusCode}",
                channelId, guildId, ex.StatusCode);
            return null;
        }
    }

    // Replaces the whole message, not just its content line. Editing content alone left a
    // finished raid alert showing its original "is being raided" embed under a line saying it had
    // ended — with the red terminate button still live, still clickable. Components have to be
    // passed explicitly (an empty list clears them) because a PATCH leaves anything omitted alone,
    // which is exactly how that button survived.
    public async Task EditPublicAsync(ulong channelId, ulong messageId, string content,
        EmbedProperties? embed = null, IReadOnlyList<IMessageComponentProperties>? components = null)
    {
        try
        {
            await gatewayClient.Rest.ModifyMessageAsync(channelId, messageId, m =>
            {
                m.Content = content;
                m.Embeds = embed is null ? [] : [embed];
                m.Components = components ?? [];
            });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            logger.LogWarning("Could not edit notification message {MessageId} in channel {ChannelId}: {StatusCode}",
                messageId, channelId, ex.StatusCode);
        }
    }

    public Task<ulong?> SendDirectMessageAsync(ulong userId, string content, ButtonProperties? terminateButton = null, EmbedProperties? embed = null) =>
        SendDirectMessageAsync(userId, content, terminateButton is null ? null : [new ActionRowProperties([terminateButton])], embed);

    // Like SendDirectMessageAsync above but for a DM carrying a full set of interactive rows (e.g. a
    // confirm/decline button pair) rather than a single terminate button — used by MemberOnboarding's
    // player-confirmation outreach. Returns null (and logs) if the member's DMs are closed.
    public async Task<ulong?> SendDirectMessageAsync(ulong userId, string content, IReadOnlyList<ActionRowProperties>? rows, EmbedProperties? embed = null)
    {
        try
        {
            var dmChannel = await gatewayClient.Rest.GetDMChannelAsync(userId);
            var message = await gatewayClient.Rest.SendMessageAsync(dmChannel.Id,
                new MessageProperties
                {
                    Content = content,
                    Embeds = embed is null ? null : [embed],
                    Components = rows,
                });
            return message.Id;
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            logger.LogInformation("Could not DM user {UserId}: {StatusCode}", userId, ex.StatusCode);
            return null;
        }
    }

    // Throttles repeat admin notifications for the same (guild, action, channel) — called from
    // recurring Quartz jobs (every 5-15 min) as well as one-shot user actions (Tickets, RoE), so a
    // persistent misconfiguration would otherwise re-notify on every single run forever.
    //
    // Escalating rather than a flat interval, because the two audiences pull in opposite directions:
    // an admin watching the channel wants to know NOW, and a guild that is going to ignore it for a
    // week should not collect 144 identical embeds a day. So: immediately, then 10 minutes, 30, an
    // hour, and hourly after that.
    private static readonly TimeSpan[] NotifyBackoff =
    [
        TimeSpan.Zero,
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1),
    ];

    // Keyed by (guild, action, channel). It used to be keyed by the LOCALIZED action text, which
    // meant two different channels failing the same action shared one hourly slot — only the first
    // was ever reported — and changing the guild's language silently reset every throttle.
    //
    // Static/process-lifetime is deliberate and sufficient — a hobby-scale, single-instance bot, and
    // a restart is itself a reasonable point to re-notify if the problem is still there.
    private static readonly ConcurrentDictionary<(ulong GuildId, BotAction Action, ulong? ChannelId), (DateTimeOffset SentAt, int Count)> LastNotifiedAt = new();

    // Called when the thing that was failing works again, so a problem that comes back is reported
    // straight away instead of resuming at the back of the backoff.
    public static void ClearPermissionIssue(ulong guildId, BotAction action, ulong? channelId) =>
        LastNotifiedAt.TryRemove((guildId, action, channelId), out _);

    // Surfaces a permission problem to a human in Discord instead of only a server-side log line
    // nobody in the guild ever sees, naming the exact permission and channel rather than a
    // hand-written guess at the cause.
    //
    // Goes to the LOG channel, not the admin channel: this is bot activity, and it belongs with the
    // rest of the activity record. The admin channel is for things asking a human to act (the STFC
    // news date confirmations), and a permission report burying those was the wrong trade.
    //
    // Call it from a catch block once an action has actually failed — this reports, it does not
    // decide. The narrowing below runs only after a real failure and only affects wording, where
    // being wrong is cosmetic and the fallback is the full requirement.
    //
    // Reporting is the LAST line of defence, not the only one. Before it come: don't reissue a call
    // that just failed (ChannelCooldown), and don't make one you already know will fail
    // (PermissionGuard for guild-level facts, ChannelAccessEvaluator where a channel check is worth
    // it). Discord's rate-limit guide asks for both, and counts 401/403/429 toward a
    // 10,000-per-10-minutes ban threshold. See CONTRIBUTING "Discord API limits".
    //
    // required: the bits THAT call needed, not the channel's whole profile — a Manage Threads
    // failure should not tell an admin to grant Embed Links.
    public async Task NotifyAdminOfPermissionIssueAsync(
        ulong guildId, BotAction action, ulong? channelId, BotPermission required)
    {
        // Logged unconditionally, before the throttle and before any channel lookup. Thirteen catch
        // blocks call this and then return without logging anything themselves — they were relying
        // entirely on the Discord message getting through. When it doesn't (no log channel set, or
        // the log channel is part of the same permission gap) the failure was invisible everywhere.
        logger.LogWarning("Permission issue in guild {GuildId}: cannot {Action}{Channel}, needs {Required}",
            guildId, action, channelId is { } c ? $" in channel {c}" : "", required);

        var key = (guildId, action, channelId);
        var now = DateTimeOffset.UtcNow;
        var previous = LastNotifiedAt.TryGetValue(key, out var last) ? last : default;
        if (previous.Count > 0 && now - previous.SentAt < NotifyBackoff[Math.Min(previous.Count - 1, NotifyBackoff.Length - 1)])
            return;

        LastNotifiedAt[key] = (now, previous.Count + 1);

        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings?.LogChannelId is not { } logChannelId)
            return;

        // The log channel being in the same permission gap is the common case, not an edge one.
        if (cooldown.IsCoolingDown(logChannelId, BotAction.SendAlert))
            return;

        try
        {
            var lang = await languageResolver.ForGuildAsync(guildId);
            var missing = await NarrowToActuallyMissingAsync(guildId, channelId, required);
            var permissions = Msg.Perm.List(lang, missing);
            var body = channelId is { } target
                ? Msg.Notify.PermissionIssueChannel(lang, Msg.Notify.Action(lang, action), $"<#{target}>", permissions)
                : Msg.Notify.PermissionIssueGuild(lang, Msg.Notify.Action(lang, action), permissions);

            var embed = await embedBranding.BuildBrandedAsync(guildId,
                $"{body}\n\n{Msg.Notify.PermissionIssueHelp(lang)}",
                EmbedBranding.DangerColor);
            await gatewayClient.Rest.SendMessageAsync(logChannelId, new MessageProperties { Embeds = [embed] });
            cooldown.RecordSuccess(logChannelId, BotAction.SendAlert);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            cooldown.RecordFailure(logChannelId, BotAction.SendAlert);
            logger.LogWarning("Could not write the permission issue to log channel {ChannelId} for guild {GuildId}: {StatusCode}",
                logChannelId, guildId, ex.StatusCode);
        }
    }

    // Best-effort: report only the bits the bot genuinely lacks, so an admin isn't sent to grant
    // four permissions when one is missing. Any failure to work it out returns the full requirement
    // — which is exactly what this reported before, so a wrong answer degrades rather than lies.
    //
    // Threads are the awkward case: a ticket/report thread is not in the guild channel list and
    // GetChannelPermissions does not accept one, so resolve the parent channel instead. If that
    // cannot be found either, don't guess.
    private async Task<BotPermission> NarrowToActuallyMissingAsync(ulong guildId, ulong? channelId, BotPermission required)
    {
        if (channelId is not { } target)
            return required;

        try
        {
            if (!gatewayClient.Cache.Guilds.TryGetValue(guildId, out var guild))
                return required;

            var bot = guild.Users.TryGetValue(gatewayClient.Id, out var cached)
                ? cached
                : await gatewayClient.Rest.GetGuildUserAsync(guildId, gatewayClient.Id);

            // A thread is not in guild.Channels and GetChannelPermissions cannot take one, so
            // resolve the channel it lives under. If neither is known, don't guess.
            var resolved = guild.Channels.ContainsKey(target) ? target
                : guild.ActiveThreads.TryGetValue(target, out var thread) && thread.ParentId is { } parentId ? parentId
                : (ulong?)null;

            if (resolved is not { } channelToCheck || !guild.Channels.ContainsKey(channelToCheck))
                return required;

            var missing = required & ~bot.GetChannelPermissions(guild, channelToCheck).ToDomain();

            return missing == BotPermission.None ? required : missing;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not narrow the missing permissions for channel {ChannelId} in guild {GuildId}", target, guildId);
            return required;
        }
    }
}
