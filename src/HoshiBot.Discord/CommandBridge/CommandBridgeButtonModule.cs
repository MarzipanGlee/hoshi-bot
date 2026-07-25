using HoshiBot.Data;
using HoshiBot.Discord.Announcements;
using HoshiBot.Discord.RoeViolations;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.CommandBridge;

public class CommandBridgeButtonModule(AlertService alertService, AnnouncementService announcementService, RoeViolationService roeViolationService,
    PendingModalInputService pendingModalInputService, GuildFeatureService featureService, GuildAllianceService allianceService, EmbedBranding embedBranding)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    // Phase 1: the Alliance audience resolves to the guild's primary linked alliance. Other
    // audiences are guild-scoped (null). Returns (scope, missing) — missing is true only when
    // the Alliance audience has no linked alliance, so the caller can treat it as disabled.
    private async Task<(int? GuildAllianceId, bool Missing)> ResolveScopeAsync(ulong guildId, GuildAudience audience)
    {
        if (audience != GuildAudience.Alliance)
            return (null, false);
        var primary = await allianceService.GetPrimaryIdAsync(guildId);
        return (primary, primary is null);
    }

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
        if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.RaidAlerts) is { } msg)
            return EphemeralReply.Of(msg);

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
        if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.ShieldReminders) is { } msg)
            return InteractionCallback.Message(EphemeralReply.Of(msg));

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
        var (guildAllianceId, scopeMissing) = await ResolveScopeAsync(guildId, parsedAudience);
        var ticketsEnabled = !scopeMissing && await featureService.IsEnabledAsync(guildId, GuildFeature.Tickets, parsedAudience, guildAllianceId);
        var anonymousEnabled = !scopeMissing && await featureService.IsEnabledAsync(guildId, GuildFeature.AnonymousMessaging, parsedAudience, guildAllianceId);

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
            CommanderName.Greeting(Context.User) + "Du hast verschiedene Möglichkeiten, eine Nachricht an den Führungsstab zu senden:\n\n" + string.Join('\n', lines),
            [new ActionRowProperties(buttons)],
            title: "Führungsstab kontaktieren");
    }

    [ComponentInteraction("ticket-open")]
    public async Task<InteractionCallbackProperties> OpenTicketPrompt(string audience)
    {
        var parsedAudience = Enum.Parse<GuildAudience>(audience);
        var (guildAllianceId, scopeMissing) = await ResolveScopeAsync(Context.Guild!.Id, parsedAudience);
        if (scopeMissing || !await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.Tickets, parsedAudience, guildAllianceId))
            return InteractionCallback.Message(EphemeralReply.Of(GuildFeatureService.DisabledMessage(GuildFeature.Tickets)));

        return InteractionCallback.Modal(new ModalProperties($"ticket-open-modal:{audience}", "Ticket öffnen",
        [
            new LabelProperties("Betreff",
                new TextInputProperties("subject", TextInputStyle.Short) { Placeholder = "Kurzen Betreff eingeben", MaxLength = 50, Required = true }),
        ]));
    }

    [ComponentInteraction("raid-terminate")]
    public Task TerminateRaid(ulong guildId, ulong targetUserId) =>
        Context.Interaction.SendDelayedResponseAsync(() => alertService.TerminateRaidAsync(guildId, Context.User.Id, targetUserId));

    [ComponentInteraction("shield-reminder-terminate")]
    public Task TerminateShieldReminder(ulong guildId) =>
        Context.Interaction.SendDelayedResponseAsync(async () =>
        {
            var result = await alertService.TerminateShieldReminderAsync(guildId, Context.User.Id);
            // The reminder is closed — remove the warning DM this "Beenden" button was on so a resolved
            // reminder doesn't keep sitting in the user's DMs. The ephemeral confirmation still shows.
            if (Context.Interaction.Message is { } warning)
            {
                try { await warning.DeleteAsync(); }
                catch (RestException) { /* already gone / not deletable — leave it, the confirmation covers it */ }
            }
            return result;
        });

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

        await Context.Interaction.SendDelayedEditAsync(async () =>
        {
            var unread = await announcementService.GetUnreadAsync(guildId, Context.User.Id);

            if (unread.Count == 0)
            {
                var doneEmbed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, CommanderName.Address(Context.User, "Du hast alle Ankündigungen gelesen. 🎉"), title: "Ungelesene Ankündigungen");
                return m => { m.Embeds = [doneEmbed]; m.Components = []; };
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

            var finalEmbed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, CommanderName.Greeting(Context.User) + "Deine ungelesenen Ankündigungen:\n" + string.Join('\n', lines), title: "Ungelesene Ankündigungen");
            return m => { m.Embeds = [finalEmbed]; m.Components = rows; };
        });
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
        if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.RoeViolationReports) is { } msg)
            return EphemeralReply.Of(msg);

        var buttons = new List<ButtonProperties>
        {
            new("roe-violation-to", "An mir", ButtonStyle.Primary),
            new("roe-violation-from", "Von mir", ButtonStyle.Primary),
        };

        if (await roeViolationService.IsCommandStaffAsync(Context.Guild!.Id, Context.User.Id))
            buttons.Add(new ButtonProperties("roe-violation-other", "Von eigenem Spieler", ButtonStyle.Secondary));

        return await EphemeralEmbedAsync(
            CommanderName.Address(Context.User, "wurde der Verstoss an Dir oder von Dir begangen?"),
            [new ActionRowProperties(buttons)],
            title: "RoE-Verstoss melden");
    }

    [ComponentInteraction("roe-violation-to")]
    public InteractionCallbackProperties<ModalProperties> ReportRoeViolationTo() =>
        InteractionCallback.Modal(RoeViolationService.Modal("to", 0));

    [ComponentInteraction("roe-violation-from")]
    public InteractionCallbackProperties<ModalProperties> ReportRoeViolationFrom() =>
        InteractionCallback.Modal(RoeViolationService.Modal("from", 0));

    [ComponentInteraction("roe-violation-other")]
    public Task<InteractionCallbackProperties<MessageOptions>> ReportRoeViolationOtherPrompt() =>
        EphemeralEmbedModifyAsync(CommanderName.Address(Context.User, "wähle den Commander, der den Verstoss begangen hat."), [new UserMenuProperties("roe-violation-other-target")]);

    [ComponentInteraction("anonymous-message")]
    public async Task<InteractionCallbackProperties> AnonymousMessagePrompt(string audience)
    {
        var parsedAudience = Enum.Parse<GuildAudience>(audience);
        var (guildAllianceId, scopeMissing) = await ResolveScopeAsync(Context.Guild!.Id, parsedAudience);
        if (scopeMissing || !await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.AnonymousMessaging, parsedAudience, guildAllianceId))
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
        var (description, components) = await BuildAlertsManageAsync();
        return await EphemeralEmbedAsync(description, components, title: "Benachrichtigungen verwalten");
    }

    // Always a button within alerts-manage's own ephemeral message — ModifyMessage is safe.
    [ComponentInteraction("alerts-toggle")]
    public async Task<InteractionCallbackProperties<MessageOptions>> ToggleAlerts(string key)
    {
        await alertService.ToggleOptInRoleAsync(Context.Guild!.Id, Context.User.Id, key);
        var (description, components) = await BuildAlertsManageAsync();
        return await EphemeralEmbedModifyAsync(description, components, title: "Benachrichtigungen verwalten");
    }

    // The opt-in status list: the alerts role plus the four ClientRelease platform roles that are
    // configured and enabled, each with a toggle button reflecting the member's current state
    // (up to five buttons — Discord allows five per action row).
    private async Task<(string Description, IReadOnlyList<IMessageComponentProperties> Components)> BuildAlertsManageAsync()
    {
        var roles = await alertService.GetOptInRolesAsync(Context.Guild!.Id, Context.User.Id);
        if (roles.Count == 0)
            return ("Es sind aktuell keine Opt-In-Rollen konfiguriert (siehe Guild-Einstellungen).", []);

        var lines = roles.Select(r => $"- **{r.Label}**: {(r.HasRole ? "EIN ✅" : "AUS ❌")}");
        var description = "Wähle, welche Benachrichtigungen Du erhalten möchtest — tippe auf eine Rolle, um sie umzuschalten:\n\n"
            + string.Join("\n", lines);

        var buttons = roles
            .Select(r => new ButtonProperties($"alerts-toggle:{r.Key}", r.Label, r.HasRole ? ButtonStyle.Success : ButtonStyle.Secondary))
            .ToList();

        return (description, [new ActionRowProperties(buttons)]);
    }
}
