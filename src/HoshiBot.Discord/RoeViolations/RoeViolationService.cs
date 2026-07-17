using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.RoeViolations;

// Posts as a forum thread (the configured channel is a Forum channel — see
// RoeViolationReportsEditor's ChannelKind.Forum picker) rather than a private thread under a
// text channel like TicketService: a Forum channel's "create thread" endpoint requires the
// starter message up front (CreateForumGuildThreadAsync), it can't be created empty and filled
// in after the way CreateGuildThreadAsync works for a normal text channel. Otherwise mirrors
// TicketService's shape — same permission-catch-and-notify-admin pattern via
// NotificationDispatcher.
public class RoeViolationService(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    NotificationDispatcher dispatcher,
    EmbedBranding embedBranding,
    GuildFeatureSettingsService settingsService,
    GuildAllianceService allianceService)
{
    private const string VictimSteps =
        "1. Überprüfe den entstandenen Verlust genau.\n" +
        "2. War es mit Sicherheit keine Zero-Node? Öffne den Chat des Angreifers, um dies zu verifizieren.\n" +
        "3. Kontaktiere den Angreifer direkt und bitte um Aufklärung. Gib ihm ein paar Stunden Zeit, um reagieren zu können.\n" +
        "4. Wird keine Einigung erzielt, poste hier Screenshots mit allen relevanten Informationen. Dazu gehören Kampf, Kampfprotokoll und Kommunikation mit dem Spieler.";

    private const string OffenderSteps =
        "1. Kontaktiere die betroffene Partei direkt und kläre den Vorfall in gegenseitigem Einvernehmen.\n" +
        "2. Wird keine Einigung erzielt, poste hier Screenshots mit allen relevanten Informationen. Dazu gehören Kampf, Kampfprotokoll und Kommunikation mit dem Spieler.";

    // Reporter-addressed instructions for the forum post. The "Commander {name}," intro and the
    // closing line are built here (not baked into the step consts) so the report's own alliance
    // diplomat role can be mentioned inline — matching the legacy post's format.
    private static string BuildInstructions(string reporterName, bool reporterIsVictim, string diplomatMention) =>
        CommanderName.Greeting(reporterName) + "danke für Deine Meldung! Bitte beachte die nachfolgenden Anweisungen und hole fehlende Punkte nach:\n\n" +
        (reporterIsVictim ? VictimSteps : OffenderSteps) +
        $"\n\nSobald Du alles erledigt hast, bestätige das mit der entsprechenden Schaltfläche unten und {diplomatMention} nimmt sich dem Fall an.";

    public static ButtonProperties ReadyButton(int reportId) =>
        new($"roe-violation-ready:{reportId}", "Alle Vorgaben erfüllt", EmojiProperties.Standard("✅"), ButtonStyle.Success);

    public static ButtonProperties DoneButton(int reportId) =>
        new($"roe-violation-done:{reportId}", "Verstoss geklärt", EmojiProperties.Standard("❌"), ButtonStyle.Danger);

    // Shared by both entry points that open this modal (CommandBridgeButtonModule for the
    // to/from branches, RoeViolationUserMenuModule for the other branch) — same 2-field
    // shape regardless of branch, only the custom-id differs. "otherUserId" is an unused
    // placeholder for the to/from branches, keeping the positional custom-id parameters
    // consistent across all 3 branches (same convention already used for Raid's
    // location-in-custom-id).
    public static ModalProperties Modal(string branch, ulong otherUserId) =>
        new($"roe-violation-modal:{branch}:{otherUserId}", "RoE-Verstoss melden",
        [
            new LabelProperties("Allianz-Tag der Gegenseite",
                new TextInputProperties("tag", TextInputStyle.Short) { Placeholder = "z.B. ABC", Required = true, MaxLength = 10 }),
            new LabelProperties("Name des Commanders der Gegenseite",
                new TextInputProperties("name", TextInputStyle.Short) { Placeholder = "Name eingeben", Required = true }),
        ]);

    // Gates the staff-only "report on behalf of an own player" option — the guild-wide Command
    // Staff role (the per-alliance Diplomat role is only for the ready-for-diplomat ping).
    public async Task<bool> IsCommandStaffAsync(ulong guildId, ulong userId)
    {
        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings?.CommandStaffRoleId is not { } roleId)
            return false;

        var guildUser = await gatewayClient.Rest.GetGuildUserAsync(guildId, userId);
        return guildUser.RoleIds.Contains(roleId);
    }

    // Resolves "this Discord user's current in-game identity" from real linked-player
    // data instead of hardcoding a single alliance tag like legacy does — falls back to
    // the caller-supplied display name + "-" tag if the member hasn't run /link-player.
    public async Task<(string Tag, string Name)> ResolveIdentityAsync(ulong userId, string fallbackDisplayName)
    {
        var userPlayer = await db.UserPlayers
            .Where(up => up.DiscordUserId == userId && up.IsMain)
            .Include(up => up.StfcPlayer).ThenInclude(p => p.Alliance)
            .FirstOrDefaultAsync();

        if (userPlayer is null)
            return ("-", fallbackDisplayName);

        return (userPlayer.StfcPlayer.Alliance?.Tag ?? "-", userPlayer.StfcPlayer.Name);
    }

    // Branded confirmation embed shown back to the reporter after the modal — matches the rest
    // of the bot's author/footer styling (the legacy report post did the same). Public so the
    // modal module's fallback branch can build one too.
    public async Task<EmbedProperties> ResultEmbedAsync(ulong guildId, string title, string description) => new()
    {
        Title = title,
        Description = description,
        Color = EmbedBranding.BotColor,
        Author = await embedBranding.BuildAuthorAsync(guildId),
        Footer = embedBranding.BuildFooter(guildId),
    };

    public async Task<EmbedProperties> CreateReportAsync(ulong guildId, ulong reporterId, string reporterDisplayName, string attackerTag, string attackerName,
        string defenderTag, string defenderName, ulong? attackerDiscordUserId, bool reporterIsVictim)
    {
        // The report belongs to the reporter's own linked alliance; if they have no resolvable
        // alliance, fall back to the guild's primary link so a report is never lost.
        var guildAllianceId = (await allianceService.FindByMemberAsync(guildId, reporterId))?.Id
            ?? await allianceService.GetPrimaryIdAsync(guildId);
        var channelIdResult = guildAllianceId is null
            ? null
            : await settingsService.GetSnowflakeAsync(
                guildId, GuildFeature.RoeViolationReports, GuildAudience.Alliance, guildAllianceId, RoeViolationReportsSettingKeys.Channel);
        if (channelIdResult is not { } channelId)
            return await ResultEmbedAsync(guildId, "RoE-Verstoss", "Der RoE-Verstoss-Kanal ist noch nicht konfiguriert (siehe Guild-Einstellungen).");

        // Mentioned inline in the instructions so the reporter knows who picks the case up; the
        // same per-alliance Diplomat role that SetReadyForDiplomatAsync pings later.
        var diplomatRoleId = guildAllianceId is null
            ? null
            : await settingsService.GetSnowflakeAsync(
                guildId, GuildFeature.Diplomacy, GuildAudience.Alliance, guildAllianceId, DiplomacySettingKeys.DiplomatRole);
        var diplomatMention = diplomatRoleId is { } diplomatId ? $"<@&{diplomatId}>" : "ein Diplomat";

        var report = new RoeViolationReport
        {
            GuildId = guildId,
            GuildAllianceId = guildAllianceId,
            AttackerAllianceTag = attackerTag,
            AttackerCommanderName = attackerName,
            DefenderAllianceTag = defenderTag,
            DefenderCommanderName = defenderName,
            ReportedByDiscordUserId = reporterId,
            Status = RoeViolationStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.RoeViolationReports.Add(report);
        await db.SaveChangesAsync();

        var threadName = $"{report.Id}-{Slugify(attackerTag)}-vs-{Slugify(defenderTag)}";
        if (threadName.Length > 100)
            threadName = threadName[..100];

        // A forum post's starter message is required at creation time — unlike a normal text
        // channel's thread (create empty, send the first message after), Discord's forum-thread
        // endpoint has no "create, then fill in" step, so the embed has to be built first.
        var embed = new EmbedProperties
        {
            Title = $"[{attackerTag}] {attackerName} - [{defenderTag}] {defenderName}",
            Description = BuildInstructions(reporterDisplayName, reporterIsVictim, diplomatMention),
            Color = EmbedBranding.BotColor,
            Author = await embedBranding.BuildAuthorAsync(guildId),
            Footer = embedBranding.BuildFooter(guildId),
        };

        // Ping the reporter (and the reported own player, if known) in the starter message so
        // they're pulled into the forum post and notified — same as pinging them by hand.
        var mentions = new List<string> { $"<@{reporterId}>" };
        if (attackerDiscordUserId is { } mentionedAttackerId && mentionedAttackerId != reporterId)
            mentions.Add($"<@{mentionedAttackerId}>");

        ForumGuildThread thread;
        try
        {
            thread = await gatewayClient.Rest.CreateForumGuildThreadAsync(channelId, new ForumGuildThreadProperties(threadName,
                new ForumGuildThreadMessageProperties
                {
                    Content = string.Join(' ', mentions),
                    Embeds = [embed],
                    Components = [new ActionRowProperties([ReadyButton(report.Id), DoneButton(report.Id)])],
                }));
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            db.RoeViolationReports.Remove(report);
            await db.SaveChangesAsync();
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, "einen RoE-Verstoss melden", $"fehlende Berechtigung (Forum-Post erstellen) in <#{channelId}>?");
            return await ResultEmbedAsync(guildId, "RoE-Verstoss", "Das RoE-Verstoss-System ist aktuell falsch konfiguriert — ein Admin wurde informiert.");
        }

        report.ThreadId = thread.Id;
        await db.SaveChangesAsync();

        try
        {
            await gatewayClient.Rest.AddGuildThreadUserAsync(thread.Id, reporterId);
            if (attackerDiscordUserId is { } attackerId && attackerId != reporterId)
                await gatewayClient.Rest.AddGuildThreadUserAsync(thread.Id, attackerId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            // The post exists even if this part fails — nothing to roll back, just report it.
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, "die RoE-Verstoss-Nutzer hinzufügen", $"fehlende Berechtigung im Thread <#{thread.Id}>?");
        }

        return await ResultEmbedAsync(guildId, "RoE-Verstoss erstellt",
            CommanderName.Address(reporterDisplayName, $"bitte wechsle in den Post <#{thread.Id}>, um Deine Meldung abzuschliessen und sie an einen Diplomaten zu übergeben."));
    }

    public async Task<string> SetReadyForDiplomatAsync(int reportId, ulong callerId)
    {
        var report = await db.RoeViolationReports.FindAsync(reportId);
        if (report is null)
            return "Meldung nicht gefunden.";
        if (callerId != report.ReportedByDiscordUserId)
            return "Nur die meldende Person kann dies bestätigen.";

        // The diplomat pinged is the one for the report's own alliance (fallback to primary for
        // legacy reports created before per-alliance scoping).
        var diplomacyAllianceId = report.GuildAllianceId ?? await allianceService.GetPrimaryIdAsync(report.GuildId);
        var diplomatRoleId = diplomacyAllianceId is null
            ? null
            : await settingsService.GetSnowflakeAsync(
                report.GuildId, GuildFeature.Diplomacy, GuildAudience.Alliance, diplomacyAllianceId, DiplomacySettingKeys.DiplomatRole);
        var mention = diplomatRoleId is { } roleId ? $"<@&{roleId}>" : null;

        try
        {
            var embed = new EmbedProperties
            {
                Description = "Der Fall ist bereit und kann übernommen werden.",
                Color = EmbedBranding.BotColor,
                Author = await embedBranding.BuildAuthorAsync(report.GuildId),
                Footer = embedBranding.BuildFooter(report.GuildId),
            };
            await gatewayClient.Rest.SendMessageAsync(report.ThreadId,
                new MessageProperties { Content = mention, Embeds = [embed] });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            await dispatcher.NotifyAdminOfPermissionIssueAsync(report.GuildId, "eine Nachricht im RoE-Verstoss-Thread senden", $"fehlende Berechtigung im Thread <#{report.ThreadId}>?");
            return "Nachricht konnte nicht gesendet werden — ein Admin wurde informiert.";
        }

        return "Diplomat wurde benachrichtigt.";
    }

    public async Task<string> CloseReportAsync(int reportId, ulong callerId)
    {
        var report = await db.RoeViolationReports.FindAsync(reportId);
        if (report is null)
            return "Meldung nicht gefunden.";
        if (callerId != report.ReportedByDiscordUserId)
            return "Nur die meldende Person kann diesen Verstoss als geklärt markieren.";
        if (report.Status == RoeViolationStatus.Closed)
            return "Diese Meldung ist bereits geschlossen.";

        try
        {
            await gatewayClient.Rest.ModifyGuildChannelAsync(report.ThreadId, o =>
            {
                o.Archived = true;
                o.Locked = true;
            });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            await dispatcher.NotifyAdminOfPermissionIssueAsync(report.GuildId, "den RoE-Verstoss-Thread schliessen", $"fehlende Manage-Threads-Berechtigung im Thread <#{report.ThreadId}>?");
            return "Verstoss konnte nicht geschlossen werden — ein Admin wurde informiert.";
        }

        report.Status = RoeViolationStatus.Closed;
        report.ClosedAt = DateTimeOffset.UtcNow;
        report.ClosedByDiscordUserId = callerId;
        await db.SaveChangesAsync();

        return "Verstoss als geklärt markiert — Thread archiviert.";
    }

    private static string Slugify(string value)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }

        return sb.ToString().Trim('-');
    }
}
