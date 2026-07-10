using HoshiBot.Data;
using HoshiBot.Discord.Announcements;
using HoshiBot.Discord.RoeViolations;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.CommandBridge;

public class CommandBridgeButtonModule(AlertService alertService, AnnouncementService announcementService, RoeViolationService roeViolationService,
    PendingModalInputService pendingModalInputService, GuildFeatureService featureService, EmbedBranding embedBranding)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    // Shared shape for every ephemeral prompt in this module — same branded style as
    // every real bot message, just also used for these interactive in-between steps.
    private async Task<InteractionMessageProperties> EphemeralEmbedAsync(string description, IReadOnlyList<IMessageComponentProperties>? components = null, string? title = null, Color? color = null)
    {
        var embed = await BuildEmbedAsync(description, title, color);
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
        var embed = await BuildEmbedAsync(description, title);
        return InteractionCallback.ModifyMessage(m =>
        {
            m.Embeds = [embed];
            m.Components = components ?? [];
        });
    }

    private async Task<EmbedProperties> BuildEmbedAsync(string description, string? title = null, Color? color = null)
    {
        var guildId = Context.Guild!.Id;
        return new EmbedProperties
        {
            Title = title,
            Description = description,
            Color = color ?? EmbedBranding.BotColor,
            Author = await embedBranding.BuildAuthorAsync(guildId),
            Footer = embedBranding.BuildFooter(guildId),
        };
    }

    [ComponentInteraction("raid-report")]
    public async Task<InteractionMessageProperties> ReportRaidPrompt()
    {
        if (!await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.RaidAlerts))
            return EphemeralReply.Of(GuildFeatureService.DisabledMessage(GuildFeature.RaidAlerts));

        return await EphemeralEmbedAsync(
            "Wähle den Commander, der geraidet wird.\n-# Tipp: Wähle Dich selbst, um den Ablauf unverbindlich auszuprobieren.",
            [new UserMenuProperties("raid-report-target")]);
    }

    [ComponentInteraction("raid-report-location-home")]
    public InteractionCallbackProperties<ModalProperties> ReportRaidLocationHome(ulong targetUserId) =>
        RaidReportModal(targetUserId, RaidServerLocation.Home);

    [ComponentInteraction("raid-report-location-enemy")]
    public InteractionCallbackProperties<ModalProperties> ReportRaidLocationEnemy(ulong targetUserId) =>
        RaidReportModal(targetUserId, RaidServerLocation.Enemy);

    private static InteractionCallbackProperties<ModalProperties> RaidReportModal(ulong targetUserId, RaidServerLocation location,
        string? system = null, string? attacker = null) =>
        InteractionCallback.Modal(new ModalProperties($"raid-report-modal:{targetUserId}:{location}", "Raid melden",
        [
            new LabelProperties("Standort",
                new TextInputProperties("system", TextInputStyle.Short) { Value = system, Placeholder = "System eingeben", Required = true }),
            new LabelProperties("Angreifer",
                new TextInputProperties("attacker", TextInputStyle.Short) { Value = attacker, Placeholder = "Optional Spieler oder Allianz eingeben", Required = false }),
        ]));

    [ComponentInteraction("shield-reminder-setup")]
    public async Task<InteractionCallbackProperties> ShieldReminderSetup()
    {
        if (!await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.ShieldReminders))
            return InteractionCallback.Message(EphemeralReply.Of(GuildFeatureService.DisabledMessage(GuildFeature.ShieldReminders)));

        return ShieldReminderModal();
    }

    private static InteractionCallbackProperties<ModalProperties> ShieldReminderModal(string? duration = null, string? system = null) =>
        InteractionCallback.Modal(new ModalProperties("shield-reminder-setup-modal", "Schilderinnerung einrichten",
        [
            new LabelProperties("Schildlaufzeit (z.B. 2d 3h 45m)",
                new TextInputProperties("duration", TextInputStyle.Short) { Value = duration, Placeholder = "z.B. 2d 3h 45m", Required = true }),
            new LabelProperties("Standort",
                new TextInputProperties("system", TextInputStyle.Short) { Value = system, Placeholder = "System eingeben", Required = true }),
        ]));

    // Message and Modal callbacks share only this non-generic base — used here so one
    // handler can return either depending on whether the pending draft still exists
    // (same shape already used by AbsenceStringMenuModule.EditTarget). These buttons only
    // ever appear on our own bot-created ephemeral retry message (never the public hub,
    // regardless of which flow created it), so the error branch can always ModifyMessage.
    [ComponentInteraction("modal-retry-back")]
    public async Task<InteractionCallbackProperties> ModalRetryBack(int pendingId)
    {
        var pending = await pendingModalInputService.GetAsync(pendingId, Context.User.Id);
        if (pending is null)
            return InteractionCallback.ModifyMessage(m => { m.Content = "Entwurf nicht gefunden (evtl. abgelaufen). Bitte den Vorgang neu starten."; m.Embeds = []; m.Components = []; });

        await pendingModalInputService.DeleteAsync(pendingId);

        return pending.Kind switch
        {
            PendingModalInputKind.ShieldReminder => ShieldReminderModal(pending.Field1, pending.Field2),
            PendingModalInputKind.RaidReport => RaidReportModal(
                ulong.Parse(pending.Field1!), Enum.Parse<RaidServerLocation>(pending.Field2!), pending.Field3, pending.Field4),
            _ => InteractionCallback.ModifyMessage(m => { m.Content = "Unbekannter Entwurfstyp."; m.Embeds = []; m.Components = []; }),
        };
    }

    [ComponentInteraction("modal-retry-cancel")]
    public async Task<InteractionCallbackProperties<MessageOptions>> ModalRetryCancel(int pendingId)
    {
        await pendingModalInputService.DeleteAsync(pendingId);
        return InteractionCallback.ModifyMessage(m => { m.Content = "Abgebrochen."; m.Embeds = []; m.Components = []; });
    }

    // The hub has one "Führungsstab kontaktieren" button per configured audience (see
    // CommandBridgeAdminModule.GetConfiguredContactAudiencesAsync) — this intermediate step
    // explains the two options (matching legacy's own two-step flow exactly) before the
    // actual ticket-open/anonymous-message buttons below, now scoped to whichever audience
    // the member clicked. Only offers whichever of the two is actually enabled for that
    // audience — the hub button itself is hidden entirely if neither is.
    [ComponentInteraction("contact-command-staff")]
    public async Task<InteractionMessageProperties> ContactCommandStaffPrompt(string audience)
    {
        var guildId = Context.Guild!.Id;
        var parsedAudience = Enum.Parse<GuildAudience>(audience);
        var ticketsEnabled = await featureService.IsEnabledAsync(guildId, GuildFeature.Tickets, parsedAudience);
        var anonymousEnabled = await featureService.IsEnabledAsync(guildId, GuildFeature.AnonymousMessaging, parsedAudience);

        var lines = new List<string>();
        var buttons = new List<ButtonProperties>();
        if (ticketsEnabled)
        {
            lines.Add("- **Ticket öffnen** - damit kannst Du mit dem Führungsstab aktiv kommunizieren, Dir kann auch geantwortet werden.");
            buttons.Add(new ButtonProperties($"ticket-open:{audience}", "Ticket öffnen", EmojiProperties.Standard("🎟️"), ButtonStyle.Primary));
        }
        if (anonymousEnabled)
        {
            lines.Add("- **Anonyme Nachricht** - Deine Nachricht wird dem Führungsstab anonym weitergeleitet, der Führungsstab kann Dir nicht antworten.");
            buttons.Add(new ButtonProperties($"anonymous-message:{audience}", "Anonyme Nachricht", EmojiProperties.Standard("📮"), ButtonStyle.Primary));
        }

        if (buttons.Count == 0)
            return EphemeralReply.Of("Diese Funktion ist auf diesem Server deaktiviert.");

        return await EphemeralEmbedAsync(
            $"Commander {CommanderName.Of(Context.User)}, Du hast verschiedene Möglichkeiten, eine Nachricht an den Führungsstab zu senden:\n\n" + string.Join('\n', lines),
            [new ActionRowProperties(buttons)],
            title: "Führungsstab kontaktieren");
    }

    [ComponentInteraction("ticket-open")]
    public async Task<InteractionCallbackProperties> OpenTicketPrompt(string audience)
    {
        var parsedAudience = Enum.Parse<GuildAudience>(audience);
        if (!await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.Tickets, parsedAudience))
            return InteractionCallback.Message(EphemeralReply.Of(GuildFeatureService.DisabledMessage(GuildFeature.Tickets)));

        return InteractionCallback.Modal(new ModalProperties($"ticket-open-modal:{audience}", "Ticket öffnen",
        [
            new LabelProperties("Betreff",
                new TextInputProperties("subject", TextInputStyle.Short) { Placeholder = "Kurzen Betreff eingeben", MaxLength = 50, Required = true }),
        ]));
    }

    [ComponentInteraction("raid-terminate")]
    public async Task<InteractionMessageProperties> TerminateRaid(ulong guildId, ulong targetUserId) =>
        EphemeralReply.Of(await alertService.TerminateRaidAsync(guildId, Context.User.Id, targetUserId));

    [ComponentInteraction("shield-reminder-terminate")]
    public async Task<InteractionMessageProperties> TerminateShieldReminder(ulong guildId) =>
        EphemeralReply.Of(await alertService.TerminateShieldReminderAsync(guildId, Context.User.Id));

    // Matches legacy's own loading-placeholder convention for this exact flow (the only
    // other one besides Absences) — an immediate "wird gesucht..." ack, then the real
    // result once the query completes. See AbsenceButtonModule.ManageAbsences for why this
    // needs manual SendResponseAsync/ModifyResponseAsync instead of a single return value.
    [ComponentInteraction("announcement-show-unread")]
    public async Task ShowUnreadAnnouncements()
    {
        var guildId = Context.Guild!.Id;
        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.Announcements))
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(EphemeralReply.Of(GuildFeatureService.DisabledMessage(GuildFeature.Announcements))));
            return;
        }

        var loadingMessage = await EphemeralEmbedAsync("Ungelesene Ankündigungen werden gesucht...",
            title: "Ungelesene Ankündigungen", color: EmbedBranding.InformationColor);
        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(loadingMessage));

        var unread = await announcementService.GetUnreadAsync(guildId, Context.User.Id);

        if (unread.Count == 0)
        {
            var doneEmbed = await BuildEmbedAsync("Du hast alle Ankündigungen gelesen. 🎉", title: "Ungelesene Ankündigungen");
            await Context.Interaction.ModifyResponseAsync(m => { m.Embeds = [doneEmbed]; m.Components = []; });
            return;
        }

        // LastKnownReadCount (kept fresh by AnnouncementCounterRefreshJob) is used here
        // rather than a.ReadReceipts.Count, since that navigation isn't loaded by
        // GetUnreadAsync's query — a small staleness window (up to ~15 min) is an
        // acceptable trade-off already established for this same count elsewhere.
        var rows = unread
            .Select(a => new ActionRowProperties([AnnouncementService.ReadButton(a.Id, a.LastKnownReadCount)]))
            .ToList();

        var lines = unread.Select(a =>
            $"{SeverityEmoji(a.Severity)} [{a.Title}](https://discord.com/channels/{a.GuildId}/{a.ChannelId}/{a.MessageId})");

        var finalEmbed = await BuildEmbedAsync("Deine ungelesenen Ankündigungen:\n" + string.Join('\n', lines), title: "Ungelesene Ankündigungen");
        await Context.Interaction.ModifyResponseAsync(m => { m.Embeds = [finalEmbed]; m.Components = rows; });
    }

    private static string SeverityEmoji(AnnouncementSeverity severity) => severity switch
    {
        AnnouncementSeverity.Elevated => "🟨",
        AnnouncementSeverity.High => "🟥",
        _ => "🟩",
    };

    [ComponentInteraction("roe-violation-report")]
    public async Task<InteractionMessageProperties> ReportRoeViolationPrompt()
    {
        if (!await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.RoeViolationReports))
            return EphemeralReply.Of(GuildFeatureService.DisabledMessage(GuildFeature.RoeViolationReports));

        var buttons = new List<ButtonProperties>
        {
            new("roe-violation-to", "An mir", ButtonStyle.Primary),
            new("roe-violation-from", "Von mir", ButtonStyle.Primary),
        };

        if (await roeViolationService.IsCommandStaffAsync(Context.Guild!.Id, Context.User.Id))
            buttons.Add(new ButtonProperties("roe-violation-other", "Von eigenem Spieler", ButtonStyle.Secondary));

        return await EphemeralEmbedAsync("Wurde der Verstoss an Dir oder von Dir begangen?", [new ActionRowProperties(buttons)]);
    }

    [ComponentInteraction("roe-violation-to")]
    public InteractionCallbackProperties<ModalProperties> ReportRoeViolationTo() =>
        InteractionCallback.Modal(RoeViolationService.Modal("to", 0));

    [ComponentInteraction("roe-violation-from")]
    public InteractionCallbackProperties<ModalProperties> ReportRoeViolationFrom() =>
        InteractionCallback.Modal(RoeViolationService.Modal("from", 0));

    [ComponentInteraction("roe-violation-other")]
    public Task<InteractionCallbackProperties<MessageOptions>> ReportRoeViolationOtherPrompt() =>
        EphemeralEmbedModifyAsync("Wähle den Commander, der den Verstoss begangen hat.", [new UserMenuProperties("roe-violation-other-target")]);

    [ComponentInteraction("anonymous-message")]
    public async Task<InteractionCallbackProperties> AnonymousMessagePrompt(string audience)
    {
        var parsedAudience = Enum.Parse<GuildAudience>(audience);
        if (!await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.AnonymousMessaging, parsedAudience))
            return InteractionCallback.Message(EphemeralReply.Of(GuildFeatureService.DisabledMessage(GuildFeature.AnonymousMessaging)));

        return InteractionCallback.Modal(new ModalProperties($"anonymous-message-modal:{audience}", "Anonyme Nachricht",
        [
            new LabelProperties("Betreff",
                new TextInputProperties("subject", TextInputStyle.Short) { Placeholder = "Kurzen Betreff eingeben", MaxLength = 100, Required = true }),
            new LabelProperties("Nachricht",
                new TextInputProperties("message", TextInputStyle.Paragraph) { Placeholder = "Deine Nachricht", Required = true }),
        ]));
    }

    [ComponentInteraction("alerts-manage")]
    public async Task<InteractionMessageProperties> AlertsManagePrompt()
    {
        if (!await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.AlertsOptIn))
            return EphemeralReply.Of(GuildFeatureService.DisabledMessage(GuildFeature.AlertsOptIn));

        var hasRole = await alertService.HasAlertsRoleAsync(Context.Guild!.Id, Context.User.Id);
        if (hasRole is null)
        {
            return await EphemeralEmbedAsync("Die Alarme-Rolle ist noch nicht konfiguriert (siehe Guild-Einstellungen).");
        }

        var status = hasRole.Value ? "EIN" : "AUS";
        return await EphemeralEmbedAsync(
            $"Die Alarme sind für Dich aktuell:\n\n- **{status}**\n\nHilf Deinen Mitspielern und schalte die Alarme wenn immer möglich ein!",
            [
                new ActionRowProperties(
                [
                    new ButtonProperties("alerts-toggle:on", "Alarme einschalten", EmojiProperties.Standard("🔔"), ButtonStyle.Success) { Disabled = hasRole.Value },
                    new ButtonProperties("alerts-toggle:off", "Alarme ausschalten", EmojiProperties.Standard("🔕"), ButtonStyle.Danger) { Disabled = !hasRole.Value },
                ]),
            ]);
    }

    // Always a button within alerts-manage's own ephemeral message — ModifyMessage is safe.
    [ComponentInteraction("alerts-toggle")]
    public async Task<InteractionCallbackProperties<MessageOptions>> ToggleAlerts(string action)
    {
        var result = await alertService.SetAlertsOptInAsync(Context.Guild!.Id, Context.User.Id, action == "on");
        return InteractionCallback.ModifyMessage(m => { m.Content = result; m.Embeds = []; m.Components = []; });
    }
}
