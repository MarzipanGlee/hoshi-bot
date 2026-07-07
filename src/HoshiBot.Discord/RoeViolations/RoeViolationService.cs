using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.RoeViolations;

// Real thread creation from scratch, mirroring TicketService's shape exactly (same
// permission-catch-and-notify-admin pattern via NotificationDispatcher).
public class RoeViolationService(HoshiBotDbContext db, GatewayClient gatewayClient, NotificationDispatcher dispatcher, EmbedBranding embedBranding)
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

    public async Task<string> CreateReportAsync(ulong guildId, ulong reporterId, string attackerTag, string attackerName,
        string defenderTag, string defenderName, ulong? attackerDiscordUserId, bool reporterIsVictim)
    {
        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings?.RoeViolationsChannelId is not { } channelId)
            return "Der RoE-Verstoss-Kanal ist noch nicht konfiguriert (siehe Guild-Einstellungen).";

        var report = new RoeViolationReport
        {
            GuildId = guildId,
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

        GuildThread thread;
        try
        {
            thread = await gatewayClient.Rest.CreateGuildThreadAsync(channelId,
                new GuildThreadProperties(threadName) { ChannelType = ChannelType.PrivateGuildThread });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            db.RoeViolationReports.Remove(report);
            await db.SaveChangesAsync();
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, "einen RoE-Verstoss melden", $"fehlende Berechtigung (Threads erstellen) in <#{channelId}>?");
            return "Das RoE-Verstoss-System ist aktuell falsch konfiguriert — ein Admin wurde informiert.";
        }

        report.ThreadId = thread.Id;
        await db.SaveChangesAsync();

        try
        {
            await gatewayClient.Rest.AddGuildThreadUserAsync(thread.Id, reporterId);
            if (attackerDiscordUserId is { } attackerId && attackerId != reporterId)
                await gatewayClient.Rest.AddGuildThreadUserAsync(thread.Id, attackerId);

            var embed = new EmbedProperties
            {
                Title = $"[{attackerTag}] {attackerName} - [{defenderTag}] {defenderName}",
                Description = reporterIsVictim ? VictimInstructions : OffenderInstructions,
                Color = EmbedBranding.BotColor,
                Author = await embedBranding.BuildAuthorAsync(guildId),
                Footer = embedBranding.BuildFooter(guildId),
            };

            await gatewayClient.Rest.SendMessageAsync(thread.Id, new MessageProperties
            {
                Embeds = [embed],
                Components = [new ActionRowProperties([ReadyButton(report.Id), DoneButton(report.Id)])],
            });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            // The thread exists even if this part fails — nothing to roll back, just report it.
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, "die RoE-Verstoss-Nachricht senden", $"fehlende Berechtigung im Thread <#{thread.Id}>?");
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

        var settings = await db.GuildSettings.FindAsync(report.GuildId);
        var mention = settings?.DiplomatRoleId is { } roleId ? $"<@&{roleId}>" : null;

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
