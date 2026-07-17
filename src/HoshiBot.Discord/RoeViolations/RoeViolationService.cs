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
    private const string VictimInstructions =
        "Bitte prüfe zuerst Folgendes, bevor der Fall weiterverfolgt wird:\n" +
        "- Überprüfe den entstandenen Verlust genau.\n" +
        "- Schliesse aus, dass es sich um einen Zero-Node-Angriff handelte.\n" +
        "- Kontaktiere den Angreifer direkt und setze eine angemessene Frist zur Klärung.\n" +
        "- Bleibt der Fall ungeklärt, sichere Beweise per Screenshot.\n\n" +
        "Sobald alle Vorgaben erfüllt sind, klicke auf \"✅ Alle Vorgaben erfüllt\". Ist der Verstoss geklärt, klicke auf \"❌ Verstoss geklärt\".";

    private const string OffenderInstructions =
        "Bitte kontaktiere die betroffene Partei und kläre den Vorfall in gegenseitigem Einvernehmen.\n\n" +
        "Sobald alle Vorgaben erfüllt sind, klicke auf \"✅ Alle Vorgaben erfüllt\". Ist der Verstoss geklärt, klicke auf \"❌ Verstoss geklärt\".";

    public static ButtonProperties ReadyButton(int reportId) =>
        new($"roe-violation-ready:{reportId}", "Alle Vorgaben erfüllt", EmojiProperties.Standard("✅"), ButtonStyle.Success);

    public static ButtonProperties DoneButton(int reportId) =>
        new($"roe-violation-done:{reportId}", "Verstoss geklärt", EmojiProperties.Standard("✖️"), ButtonStyle.Danger);

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

    // True if the user holds the diplomat role for their own alliance (the per-alliance
    // Diplomacy DiplomatRole that RoE reuses) — gates the staff-only "report on behalf of an
    // own player" option. Falls back to the guild's primary alliance for members with no
    // resolvable alliance, mirroring CreateReportAsync/SetReadyForDiplomatAsync.
    public async Task<bool> IsDiplomatAsync(ulong guildId, ulong userId)
    {
        var allianceId = (await allianceService.FindByMemberAsync(guildId, userId))?.Id
            ?? await allianceService.GetPrimaryIdAsync(guildId);
        if (allianceId is null)
            return false;

        var roleId = await settingsService.GetSnowflakeAsync(
            guildId, GuildFeature.Diplomacy, GuildAudience.Alliance, allianceId, DiplomacySettingKeys.DiplomatRole);
        if (roleId is not { } id)
            return false;

        var guildUser = await gatewayClient.Rest.GetGuildUserAsync(guildId, userId);
        return guildUser.RoleIds.Contains(id);
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

    public async Task<string> CreateReportAsync(ulong guildId, ulong reporterId, string attackerTag, string attackerName,
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
            return "Der RoE-Verstoss-Kanal ist noch nicht konfiguriert (siehe Guild-Einstellungen).";

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
            Description = reporterIsVictim ? VictimInstructions : OffenderInstructions,
            Color = EmbedBranding.BotColor,
            Author = await embedBranding.BuildAuthorAsync(guildId),
            Footer = embedBranding.BuildFooter(guildId),
        };

        ForumGuildThread thread;
        try
        {
            thread = await gatewayClient.Rest.CreateForumGuildThreadAsync(channelId, new ForumGuildThreadProperties(threadName,
                new ForumGuildThreadMessageProperties
                {
                    Embeds = [embed],
                    Components = [new ActionRowProperties([ReadyButton(report.Id), DoneButton(report.Id)])],
                }));
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            db.RoeViolationReports.Remove(report);
            await db.SaveChangesAsync();
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, "einen RoE-Verstoss melden", $"fehlende Berechtigung (Forum-Post erstellen) in <#{channelId}>?");
            return "Das RoE-Verstoss-System ist aktuell falsch konfiguriert — ein Admin wurde informiert.";
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

        return $"RoE-Verstoss gemeldet: <#{thread.Id}>";
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
