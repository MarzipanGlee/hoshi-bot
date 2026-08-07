using HoshiBot.Data;
using HoshiBot.Discord.Alerts;
using HoshiBot.Discord.Announcements;
using HoshiBot.Discord.ReadReceipts;
using HoshiBot.Discord.RoeViolations;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.CommandBridge;

public class CommandBridgeButtonModule(AlertService alertService, ReadReceiptService readReceipts, RoeViolationService roeViolationService,
    PendingModalInputService pendingModalInputService, GuildFeatureService featureService, GuildAllianceService allianceService, EmbedBranding embedBranding,
    LanguageResolver languageResolver, HoshiBotDbContext db, GuildFeatureSettingsService settingsService)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    // Ephemeral prompts and modals follow the acting user's language.
    private Task<Language> ActingUserLanguageAsync() =>
        languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

    // Shared shape for every ephemeral prompt in this module — same branded style as
    // every real bot message, just also used for these interactive in-between steps.
    private async Task<InteractionMessageProperties> EphemeralEmbedAsync(string description, IReadOnlyList<IMessageComponentProperties>? components = null, string? title = null, Color? color = null)
    {
        var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, description, color, title);
        return new InteractionMessageProperties
        {
            Embeds = [embed],
            Flags = MessageFlags.Ephemeral,
            Components = components,
        };
    }

    // Used for follow-up steps within a wizard that already posted its own ephemeral
    // message (e.g. roe-violation-report, alerts-manage) — edits that message in place
    // instead of stacking a new one.
    private async Task<InteractionCallbackProperties<MessageOptions>> EphemeralEmbedModifyAsync(string description, IReadOnlyList<IMessageComponentProperties>? components = null, string? title = null)
    {
        var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, description, title: title);
        return InteractionCallback.ModifyMessage(m =>
        {
            m.Embeds = [embed];
            m.Components = components ?? [];
        });
    }

    [ComponentInteraction("raid-report")]
    public async Task<InteractionMessageProperties> ReportRaidPrompt()
    {
        var lang = await ActingUserLanguageAsync();
        if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.RaidAlerts, lang) is { } msg)
            return await EphemeralEmbedAsync(msg);

        return await EphemeralEmbedAsync(
            Msg.Bridge.RaidTargetPrompt(lang),
            [new UserMenuProperties("raid-report-target")]);
    }

    // Async so the modal renders in the acting user's language — modals can't be deferred,
    // but the cached resolver lookup is well within Discord's 3s window.
    [ComponentInteraction("raid-report-location-home")]
    public async Task<InteractionCallbackProperties<ModalProperties>> ReportRaidLocationHome(ulong targetUserId) =>
        RaidReportModal(targetUserId, RaidServerLocation.Home, await ActingUserLanguageAsync());

    [ComponentInteraction("raid-report-location-enemy")]
    public async Task<InteractionCallbackProperties<ModalProperties>> ReportRaidLocationEnemy(ulong targetUserId) =>
        RaidReportModal(targetUserId, RaidServerLocation.Enemy, await ActingUserLanguageAsync());

    private static InteractionCallbackProperties<ModalProperties> RaidReportModal(ulong targetUserId, RaidServerLocation location,
        Language lang, string? system = null, string? attacker = null) =>
        InteractionCallback.Modal(new ModalProperties($"raid-report-modal:{targetUserId}:{location}", Msg.Bridge.RaidModalTitle(lang),
        [
            new LabelProperties(Msg.Bridge.LocationLabel(lang),
                new TextInputProperties("system", TextInputStyle.Short) { Value = system, Placeholder = Msg.Bridge.SystemPlaceholder(lang), Required = true }),
            new LabelProperties(Msg.Bridge.AttackerLabel(lang),
                new TextInputProperties("attacker", TextInputStyle.Short) { Value = attacker, Placeholder = Msg.Bridge.AttackerPlaceholder(lang), Required = false }),
        ]));

    [ComponentInteraction("shield-reminder-setup")]
    public async Task<InteractionCallbackProperties> ShieldReminderSetup()
    {
        var lang = await ActingUserLanguageAsync();
        if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.ShieldReminders, lang) is { } msg)
            return InteractionCallback.Message(await EphemeralEmbedAsync(msg));

        return ShieldReminderModal(lang);
    }

    private static InteractionCallbackProperties<ModalProperties> ShieldReminderModal(Language lang, string? duration = null, string? system = null) =>
        InteractionCallback.Modal(new ModalProperties("shield-reminder-setup-modal", Msg.Bridge.ShieldModalTitle(lang),
        [
            new LabelProperties(Msg.Bridge.ShieldDurationLabel(lang),
                new TextInputProperties("duration", TextInputStyle.Short) { Value = duration, Placeholder = Msg.Bridge.ShieldDurationPlaceholder(lang), Required = true }),
            new LabelProperties(Msg.Bridge.LocationLabel(lang),
                new TextInputProperties("system", TextInputStyle.Short) { Value = system, Placeholder = Msg.Bridge.SystemPlaceholder(lang), Required = true }),
        ]));

    // Message and Modal callbacks share only this non-generic base — used here so one
    // handler can return either depending on whether the pending draft still exists
    // (same shape already used by AbsenceStringMenuModule.EditTarget). These buttons only
    // ever appear on our own bot-created ephemeral retry message (never the public hub,
    // regardless of which flow created it), so the error branch can always ModifyMessage.
    [ComponentInteraction("modal-retry-back")]
    public async Task<InteractionCallbackProperties> ModalRetryBack(int pendingId)
    {
        var lang = await ActingUserLanguageAsync();
        var pending = await pendingModalInputService.GetAsync(pendingId, Context.User.Id);
        if (pending is null)
            return InteractionCallback.ModifyMessage(m => { m.Content = Msg.Bridge.DraftNotFound(lang); m.Embeds = []; m.Components = []; });

        await pendingModalInputService.DeleteAsync(pendingId);

        return pending.Kind switch
        {
            PendingModalInputKind.ShieldReminder => ShieldReminderModal(lang, pending.Field1, pending.Field2),
            PendingModalInputKind.RaidReport => RaidReportModal(
                ulong.Parse(pending.Field1!), Enum.Parse<RaidServerLocation>(pending.Field2!), lang, pending.Field3, pending.Field4),
            _ => InteractionCallback.ModifyMessage(m => { m.Content = Msg.Bridge.DraftUnknownKind(lang); m.Embeds = []; m.Components = []; }),
        };
    }

    [ComponentInteraction("modal-retry-cancel")]
    public async Task<InteractionCallbackProperties<MessageOptions>> ModalRetryCancel(int pendingId)
    {
        await pendingModalInputService.DeleteAsync(pendingId);
        var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, Msg.Bridge.Cancelled(await ActingUserLanguageAsync()));
        return InteractionCallback.ModifyMessage(m => { m.Content = ""; m.Embeds = [embed]; m.Components = []; });
    }

    // The hub has one "Führungsstab kontaktieren" button per configured audience (built in
    // CommandBridgeHubService from CommandBridgeCatalog's ContactStaff entry) — this
    // intermediate step explains the two options (matching legacy's own two-step flow exactly) before the
    // actual ticket-open/anonymous-message buttons below, now scoped to whichever audience
    // the member clicked. Only offers whichever of the two is actually enabled for that
    // audience — the hub button itself is hidden entirely if neither is.
    [ComponentInteraction("contact-command-staff")]
    public async Task<InteractionMessageProperties> ContactCommandStaffPrompt(string audience)
    {
        var guildId = Context.Guild!.Id;
        var lang = await ActingUserLanguageAsync();
        var (parsedAudience, guildAllianceId, scopeMissing) = await allianceService.ResolveScopeAsync(guildId, audience);
        var ticketsEnabled = !scopeMissing && await featureService.IsEnabledAsync(guildId, GuildFeature.Tickets, parsedAudience, guildAllianceId);
        var anonymousEnabled = !scopeMissing && await featureService.IsEnabledAsync(guildId, GuildFeature.AnonymousMessaging, parsedAudience, guildAllianceId);

        var lines = new List<string>();
        var buttons = new List<ButtonProperties>();
        if (ticketsEnabled)
        {
            lines.Add(Msg.Bridge.ContactTicketOption(lang));
            buttons.Add(new ButtonProperties($"ticket-open:{audience}", Msg.Bridge.TicketOpen(lang), EmojiProperties.Standard(Icons.Ticket), ButtonStyle.Primary));
        }
        if (anonymousEnabled)
        {
            lines.Add(Msg.Bridge.ContactAnonymousOption(lang));
            buttons.Add(new ButtonProperties($"anonymous-message:{audience}", Msg.Bridge.AnonymousMessage(lang), EmojiProperties.Standard(Icons.ContactStaff), ButtonStyle.Primary));
        }

        if (buttons.Count == 0)
            return await EphemeralEmbedAsync(Msg.Bridge.FeatureDisabledHere(lang));

        return await EphemeralEmbedAsync(
            Msg.Bridge.ContactIntro(lang, CommanderName.Of(Context.User), string.Join('\n', lines)),
            [new ActionRowProperties(buttons)],
            title: Msg.Bridge.ContactTitle(lang));
    }

    [ComponentInteraction("ticket-open")]
    public async Task<InteractionCallbackProperties> OpenTicketPrompt(string audience)
    {
        var lang = await ActingUserLanguageAsync();
        var (parsedAudience, guildAllianceId, scopeMissing) = await allianceService.ResolveScopeAsync(Context.Guild!.Id, audience);
        if (scopeMissing || !await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.Tickets, parsedAudience, guildAllianceId))
            return InteractionCallback.Message(await EphemeralEmbedAsync(GuildFeatureService.DisabledMessage(GuildFeature.Tickets, lang)));

        return InteractionCallback.Modal(new ModalProperties($"ticket-open-modal:{audience}", Msg.Bridge.TicketOpen(lang),
        [
            new LabelProperties(Msg.Bridge.SubjectLabel(lang),
                new TextInputProperties("subject", TextInputStyle.Short) { Placeholder = Msg.Bridge.SubjectPlaceholder(lang), MaxLength = 50, Required = true }),
        ]));
    }

    [ComponentInteraction("raid-terminate")]
    public Task TerminateRaid(ulong guildId, ulong targetUserId) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, guildId, () => alertService.TerminateRaidAsync(guildId, Context.User.Id, targetUserId));

    [ComponentInteraction("shield-reminder-terminate")]
    public Task TerminateShieldReminder(ulong guildId) =>
        Context.Interaction.SendDelayedEditAsync(async () =>
        {
            var result = await alertService.TerminateShieldReminderAsync(guildId, Context.User.Id);
            // The reminder is closed — remove the warning DM this "Beenden" button was on so a resolved
            // reminder doesn't keep sitting in the user's DMs. The ephemeral confirmation still shows.
            if (Context.Interaction.Message is { } warning)
            {
                try { await warning.DeleteAsync(); }
                catch (RestException) { /* already gone / not deletable — leave it, the confirmation covers it */ }
            }
            // Branded embed like every other real bot message, not plain text.
            var confirmation = await embedBranding.BuildBrandedAsync(guildId, result);
            return m => { m.Content = ""; m.Embeds = [confirmation]; m.Components = []; };
        });

    // Matches legacy's own loading-placeholder convention for this exact flow (the only
    // other one besides Absences) — an immediate "wird gesucht..." ack, then the real
    // result once the query completes. See AbsenceButtonModule.ManageAbsences for why this
    // needs manual SendResponseAsync/ModifyResponseAsync instead of a single return value.
    [ComponentInteraction("announcement-show-unread")]
    public async Task ShowUnreadAnnouncements()
    {
        var guildId = Context.Guild!.Id;
        var lang = await ActingUserLanguageAsync();
        // Read receipts owns this list now, not Announcements — with the feature off there is
        // nothing tracked to be unread about, whatever else the guild publishes.
        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.ReadReceipts, GuildAudience.Guild, null))
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(await EphemeralEmbedAsync(GuildFeatureService.DisabledMessage(GuildFeature.ReadReceipts, lang))));
            return;
        }

        await Context.Interaction.SendDelayedEditAsync(async () =>
        {
            var unread = await readReceipts.GetUnreadAsync(guildId, (GuildUser)Context.User);

            if (unread.Count == 0)
            {
                var doneEmbed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, Msg.Bridge.AnnouncementsAllRead(lang, CommanderName.Of(Context.User)), title: Msg.Bridge.AnnouncementsUnreadTitle(lang));
                return m => { m.Embeds = [doneEmbed]; m.Components = []; };
            }

            // LastKnownReadCount (kept fresh by AnnouncementCounterRefreshJob) is used here rather
            // than counting Receipts, since that navigation isn't loaded by GetUnreadAsync's query —
            // a small staleness window (up to ~15 min) is the trade-off already established for this
            // same count elsewhere.
            //
            // Only the ✅ here: every row already sits under a heading of unread posts, so a "show
            // unread" button per row would be circular.
            var rows = unread
                .Select(p => new ActionRowProperties([ReadReceiptService.ReadButton(p.Id, p.LastKnownReadCount, lang)]))
                .ToList();

            // The kind's own icon rather than a severity, since the list now spans announcements,
            // forwarded translations and whatever registers next.
            var lines = unread.Select(p =>
                $"{ReadReceiptService.Icon(p.Kind)} [{p.Title}](https://discord.com/channels/{p.GuildId}/{p.ChannelId}/{p.MessageId})");

            var finalEmbed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, Msg.Bridge.AnnouncementsUnreadIntro(lang, CommanderName.Of(Context.User), string.Join('\n', lines)), title: Msg.Bridge.AnnouncementsUnreadTitle(lang));
            return m => { m.Embeds = [finalEmbed]; m.Components = rows; };
        });
    }

    [ComponentInteraction("roe-violation-report")]
    public async Task<InteractionMessageProperties> ReportRoeViolationPrompt()
    {
        var lang = await ActingUserLanguageAsync();
        if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.RoeViolationReports, lang) is { } msg)
            return await EphemeralEmbedAsync(msg);

        var buttons = new List<ButtonProperties>
        {
            new("roe-violation-to", Msg.Bridge.RoeToMe(lang), ButtonStyle.Primary),
            new("roe-violation-from", Msg.Bridge.RoeFromMe(lang), ButtonStyle.Primary),
        };

        if (await roeViolationService.IsCommandStaffAsync(Context.Guild!.Id, Context.User.Id))
            buttons.Add(new ButtonProperties("roe-violation-other", Msg.Bridge.RoeByOwnPlayer(lang), ButtonStyle.Secondary));

        return await EphemeralEmbedAsync(
            Msg.Bridge.RoePromptBody(lang, CommanderName.Of(Context.User)),
            [new ActionRowProperties(buttons)],
            title: Msg.Roe.ModalTitle(lang));
    }

    // Async so the modal renders in the acting user's language — modals can't be deferred,
    // but the cached resolver lookup is well within Discord's 3s window.
    [ComponentInteraction("roe-violation-to")]
    public async Task<InteractionCallbackProperties<ModalProperties>> ReportRoeViolationTo() =>
        InteractionCallback.Modal(RoeViolationService.Modal("to", 0, await ActingUserLanguageAsync()));

    [ComponentInteraction("roe-violation-from")]
    public async Task<InteractionCallbackProperties<ModalProperties>> ReportRoeViolationFrom() =>
        InteractionCallback.Modal(RoeViolationService.Modal("from", 0, await ActingUserLanguageAsync()));

    [ComponentInteraction("roe-violation-other")]
    public async Task<InteractionCallbackProperties<MessageOptions>> ReportRoeViolationOtherPrompt() =>
        await EphemeralEmbedModifyAsync(Msg.Bridge.RoeOtherPrompt(await ActingUserLanguageAsync(), CommanderName.Of(Context.User)), [new UserMenuProperties("roe-violation-other-target")]);

    [ComponentInteraction("anonymous-message")]
    public async Task<InteractionCallbackProperties> AnonymousMessagePrompt(string audience)
    {
        var lang = await ActingUserLanguageAsync();
        var (parsedAudience, guildAllianceId, scopeMissing) = await allianceService.ResolveScopeAsync(Context.Guild!.Id, audience);
        if (scopeMissing || !await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.AnonymousMessaging, parsedAudience, guildAllianceId))
            return InteractionCallback.Message(await EphemeralEmbedAsync(GuildFeatureService.DisabledMessage(GuildFeature.AnonymousMessaging, lang)));

        return InteractionCallback.Modal(new ModalProperties($"anonymous-message-modal:{audience}", Msg.Bridge.AnonymousMessage(lang),
        [
            new LabelProperties(Msg.Bridge.SubjectLabel(lang),
                new TextInputProperties("subject", TextInputStyle.Short) { Placeholder = Msg.Bridge.SubjectPlaceholder(lang), MaxLength = 100, Required = true }),
            new LabelProperties(Msg.Bridge.MessageLabel(lang),
                new TextInputProperties("message", TextInputStyle.Paragraph) { Placeholder = Msg.Bridge.MessagePlaceholder(lang), Required = true }),
        ]));
    }

    [ComponentInteraction("alerts-manage")]
    public async Task<InteractionMessageProperties> AlertsManagePrompt()
    {
        var lang = await ActingUserLanguageAsync();
        var (description, components) = await BuildAlertsManageAsync(lang);
        return await EphemeralEmbedAsync(description, components, title: Msg.Bridge.AlertsTitle(lang));
    }

    // Always a button within alerts-manage's own ephemeral message — ModifyMessage is safe.
    [ComponentInteraction("alerts-toggle")]
    public async Task<InteractionCallbackProperties<MessageOptions>> ToggleAlerts(string key)
    {
        var lang = await ActingUserLanguageAsync();
        await alertService.ToggleOptInRoleAsync(Context.Guild!.Id, Context.User.Id, key);
        var (description, components) = await BuildAlertsManageAsync(lang);
        return await EphemeralEmbedModifyAsync(description, components, title: Msg.Bridge.AlertsTitle(lang));
    }

    // The opt-in status list: the alerts role plus the four ClientRelease platform roles that are
    // configured and enabled, each with a toggle button reflecting the member's current state
    // (up to five buttons — Discord allows five per action row).
    private async Task<(string Description, IReadOnlyList<IMessageComponentProperties> Components)> BuildAlertsManageAsync(Language lang)
    {
        var roles = await alertService.GetOptInRolesAsync(Context.Guild!.Id, Context.User.Id);
        if (roles.Count == 0)
            return (Msg.Bridge.AlertsNoRoles(lang), []);

        var lines = roles.Select(r => $"- **{r.Label}**: {(r.HasRole ? Msg.Bridge.AlertsOn(lang) : Msg.Bridge.AlertsOff(lang))}");
        var description = Msg.Bridge.AlertsIntro(lang, string.Join("\n", lines));

        var buttons = roles
            .Select(r => new ButtonProperties($"alerts-toggle:{r.Key}", r.Label, r.HasRole ? ButtonStyle.Success : ButtonStyle.Secondary))
            .ToList();

        return (description, [new ActionRowProperties(buttons)]);
    }

    // ---- Help buttons -----------------------------------------------------------------------
    //
    // Two buttons, two different questions, which is why they are two features: "help with
    // something else" points at the members who can answer an in-game question, "help" points at
    // whoever looks after the bot. Both only ever *mention* a channel and post nothing, so neither
    // declares a channel permission slot — a couple of indexed reads, answered inline rather than
    // through the deferred path.

    [ComponentInteraction("channel-guide")]
    public async Task<InteractionMessageProperties> ChannelGuide()
    {
        var lang = await ActingUserLanguageAsync();
        var allianceId = await ClickedAllianceIdAsync();
        if (await AllianceScopedGuardAsync(GuildFeature.ChannelGuide, allianceId, lang) is { } disabled)
            return await EphemeralEmbedAsync(disabled);

        var message = await settingsService.GetTextAsync(
            Context.Guild!.Id, GuildFeature.ChannelGuide, GuildAudience.Alliance, allianceId, ChannelGuideSettingKeys.Message);

        // An enabled feature with no text written yet would otherwise render an empty embed, which
        // reads as the bot being broken rather than as the admins not having filled it in.
        // The body is one fixed string an admin wrote in ONE language, so the title has to follow
        // the same scope rather than the reader — otherwise an English member opens an English
        // "Request support" heading over a German message, which is how this first shipped.
        var bodyLang = allianceId is { } id
            ? await languageResolver.ForAllianceAsync(id)
            : await languageResolver.ForGuildAsync(Context.Guild!.Id);

        var body = string.IsNullOrWhiteSpace(message)
            ? Msg.Bridge.ChannelGuideNotConfigured(bodyLang)
            : message.Replace(CommanderPlaceholder, CommanderName.Of(Context.User));

        return await EphemeralEmbedAsync(body, title: Msg.Bridge.ChannelGuideTitle(bodyLang));
    }

    [ComponentInteraction("bot-support")]
    public async Task<InteractionMessageProperties> BotSupport()
    {
        var lang = await ActingUserLanguageAsync();
        var allianceId = await ClickedAllianceIdAsync();
        if (await AllianceScopedGuardAsync(GuildFeature.BotSupport, allianceId, lang) is { } disabled)
            return await EphemeralEmbedAsync(disabled);

        var channelId = await settingsService.GetSnowflakeAsync(
            Context.Guild!.Id, GuildFeature.BotSupport, GuildAudience.Alliance, allianceId, BotSupportSettingKeys.Channel);

        var commander = CommanderName.Of(Context.User);
        var body = channelId is { } id
            ? Msg.Bridge.BotSupportBody(lang, commander, $"<#{id}>")
            : Msg.Bridge.BotSupportNoChannel(lang, commander);

        return await EphemeralEmbedAsync(body, title: Msg.Bridge.BotSupportTitle(lang));
    }

    // What an admin writes in the channel-guide text to address the member by name.
    private const string CommanderPlaceholder = "{commander}";

    // Both features are Alliance-audience, so "enabled" is per linked alliance.
    private async Task<string?> AllianceScopedGuardAsync(GuildFeature feature, int? guildAllianceId, Language lang) =>
        await featureService.IsEnabledAsync(Context.Guild!.Id, feature, GuildAudience.Alliance, guildAllianceId)
            ? null
            : GuildFeatureService.DisabledMessage(feature, lang);

    // The user bridge is posted once per linked alliance, so the channel the click came from
    // identifies it. Null when the click came from somewhere else (a moved message, a shared
    // channel): the settings lookup then finds nothing and the caller falls back to its "not
    // configured" copy rather than showing another alliance's channels.
    private async Task<int?> ClickedAllianceIdAsync() =>
        await db.GuildAlliances
            .Where(a => a.GuildId == Context.Guild!.Id && a.CommandBridgeChannelId == Context.Channel.Id)
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync();
}
