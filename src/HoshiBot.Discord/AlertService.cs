using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord;

// Core raid/shield-reminder logic shared between the slash commands (AlertModule) and
// the Command Bridge button/modal flow (CommandBridgeModule) — both are valid entry
// points to the same operations.
public class AlertService(HoshiBotDbContext db, NotificationDispatcher dispatcher, GatewayClient gatewayClient, EmbedBranding embedBranding)
{
    // Buttons can be clicked from a DM (no NetCord Guild context there), so the guild ID
    // travels in the custom_id itself rather than relying on Context.Guild.
    public static ButtonProperties RaidTerminateButton(ulong guildId, ulong targetUserId) =>
        new($"raid-terminate:{guildId}:{targetUserId}", "Beenden", ButtonStyle.Danger);

    public static ButtonProperties ShieldReminderTerminateButton(ulong guildId) =>
        new($"shield-reminder-terminate:{guildId}", "Beenden", ButtonStyle.Danger);

    // Case-insensitive: EF Core translates ToUpper() to the SQL UPPER() function on both
    // providers. On Npgsql (production) this is a real Unicode-aware case fold; on SQLite
    // (local dev) UPPER() only handles ASCII by default — a known, already-accepted
    // dev/prod discrepancy in this codebase (see e.g. DateTimeOffset-ordering comments
    // elsewhere), and moot today since every synced system name is English/ASCII anyway.
    //
    // Falls back to a phonetic match (SystemNamePhoneticKey) when the exact lookup
    // misses — primarily for Cyrillic-transliterated input from Russian-locale players
    // (e.g. "Донифон" for "Doniphon", confirmed against a real example; there's no
    // localized-name data source to match against directly, only the game's own
    // phonetic transliteration, which this reverses on a best-effort basis). Scanning all
    // ~2,600 systems in memory is cheap for this low-frequency, exact-match-first lookup —
    // no phonetic algorithm translates to SQL anyway.
    public async Task<StfcSystem?> FindSystemByNameAsync(string name)
    {
        var exact = await db.StfcSystems.FirstOrDefaultAsync(s => s.Name.ToUpper() == name.ToUpper());
        if (exact is not null)
            return exact;

        var key = SystemNamePhoneticKey.Compute(name);
        var all = await db.StfcSystems.ToListAsync();
        return all.FirstOrDefault(s => SystemNamePhoneticKey.Compute(s.Name) == key);
    }

    public async Task<string> ReportRaidAsync(ulong guildId, ulong triggeredByUserId, ulong targetUserId,
        string system, RaidServerLocation serverLocation, string? attacker)
    {
        var now = DateTimeOffset.UtcNow;

        // Targeting yourself is never a real raid (you wouldn't self-report while
        // actually being raided) — a safe, free signal for "just trying the flow out."
        var isTest = targetUserId == triggeredByUserId;

        var stfcSystem = await FindSystemByNameAsync(system);
        if (stfcSystem is null)
            return $"Unbekanntes System \"{system}\". Bitte die Schreibweise prüfen.";

        var active = await db.Alerts.FirstOrDefaultAsync(a =>
            a.GuildId == guildId && a.Type == AlertType.Raid && a.TargetDiscordUserId == targetUserId && a.TerminatedAt == null);
        if (active is not null)
            return $"<@{targetUserId}> was already reported by <@{active.TriggeredByDiscordUserId}> at <t:{active.TriggeredAt.ToUnixTimeSeconds()}:f>.";

        // Excludes test runs, so a self-test never shows up in real history. Ordered
        // client-side: SQLite's EF Core provider can't translate DateTimeOffset
        // ordering/comparisons — same workaround already used elsewhere in this codebase
        // (e.g. ThreadCleanupJob) — and per-target raid counts are small enough that this
        // is cheap either way.
        var pastRaids = (await db.Alerts
            .Include(a => a.StfcSystem)
            .Where(a => a.GuildId == guildId && a.Type == AlertType.Raid && a.TargetDiscordUserId == targetUserId
                && a.TerminatedAt != null && !a.IsTest)
            .ToListAsync())
            .OrderByDescending(a => a.TriggeredAt)
            .Take(5)
            .ToList();

        var alert = new Alert
        {
            GuildId = guildId,
            Type = AlertType.Raid,
            TargetDiscordUserId = targetUserId,
            StfcSystemId = stfcSystem.Id,
            Attacker = attacker,
            ServerLocation = serverLocation,
            IsTest = isTest,
            TriggeredByDiscordUserId = triggeredByUserId,
            TriggeredAt = now,
        };
        db.Alerts.Add(alert);
        await db.SaveChangesAsync();

        var terminateButton = RaidTerminateButton(guildId, targetUserId);

        var fields = new List<EmbedFieldProperties>
        {
            new() { Name = "System", Value = stfcSystem.Name, Inline = true },
            new() { Name = "Ziel", Value = $"<@{targetUserId}>", Inline = true },
            new() { Name = "Server", Value = ServerLocationLabel(serverLocation), Inline = true },
            new() { Name = "Angreifer", Value = attacker ?? "-", Inline = true },
            new() { Name = "Gemeldet", Value = $"<t:{now.ToUnixTimeSeconds()}:f> von <@{triggeredByUserId}>", Inline = true },
        };
        if (pastRaids.Count > 0)
        {
            var lines = pastRaids.Select(a =>
                $"- <t:{a.TriggeredAt.ToUnixTimeSeconds()}:f> ({(int)(a.TerminatedAt!.Value - a.TriggeredAt).TotalMinutes} Min.) in {a.StfcSystem?.Name}");
            fields.Add(new EmbedFieldProperties { Name = "Vergangene Raids", Value = string.Join('\n', lines) });
        }

        var embed = new EmbedProperties
        {
            Title = "Raid Alarm",
            Description = $"Commander, die Station von Commander <@{targetUserId}> wird geraidet!",
            Color = EmbedBranding.DangerColor,
            Fields = fields,
            Author = await embedBranding.BuildAuthorAsync(guildId),
            Footer = embedBranding.BuildFooter(guildId),
        };

        var publicContent = $"Commander, <@{targetUserId}> wird geraidet!";

        if (isTest)
        {
            var alertChannel = await db.GuildAlertChannels.FirstOrDefaultAsync(c => c.GuildId == guildId && c.Kind == GuildAlertChannelKind.Raid);
            var hint = alertChannel is not null
                ? $"Diese Nachricht würde normal in <#{alertChannel.ChannelId}> gesendet\n\n"
                : "";
            var testMessageId = await dispatcher.SendDirectMessageAsync(targetUserId, hint + publicContent, terminateButton, embed);
            alert.Notifications.Add(new AlertNotification
            {
                Kind = NotificationKind.Public,
                ChannelId = targetUserId,
                MessageId = testMessageId,
                SentAt = now,
            });
        }
        else
        {
            var publicResults = await dispatcher.SendPublicAsync(guildId, GuildAlertChannelKind.Raid, publicContent, terminateButton, embed);
            foreach (var (channelId, messageId) in publicResults)
            {
                alert.Notifications.Add(new AlertNotification
                {
                    Kind = NotificationKind.Public,
                    ChannelId = channelId,
                    MessageId = messageId,
                    SentAt = now,
                });
            }
        }

        var dmEmbed = new EmbedProperties
        {
            Description = $"Commander, you're being raided in **{stfcSystem.Name}**! Use the button below or /raid-terminate once it's resolved.",
            Color = EmbedBranding.DangerColor,
            Author = await embedBranding.BuildAuthorAsync(guildId),
            Footer = embedBranding.BuildFooter(guildId),
        };
        var dmMessageId = await dispatcher.SendDirectMessageAsync(targetUserId, "", terminateButton, dmEmbed);
        alert.Notifications.Add(new AlertNotification
        {
            Kind = NotificationKind.User,
            ChannelId = targetUserId,
            MessageId = dmMessageId,
            SentAt = now,
        });

        await db.SaveChangesAsync();

        return isTest
            ? "Testlauf abgeschlossen — Du hast zwei DMs erhalten (persönliche Warnung + Vorschau des öffentlichen Alarms). Dieser Testlauf erscheint nicht in der echten Raid-Historie."
            : $"Raid reported for <@{targetUserId}> in {stfcSystem.Name}.";
    }

    private static string ServerLocationLabel(RaidServerLocation location) => location switch
    {
        RaidServerLocation.Home => "Home",
        RaidServerLocation.Enemy => "Enemy",
        _ => "-",
    };

    public async Task<string> TerminateRaidAsync(ulong guildId, ulong callerId, ulong targetId)
    {
        if (targetId != callerId)
        {
            var settings = await db.GuildSettings.FindAsync(guildId);
            var isCommandStaff = false;
            if (settings?.CommandStaffRoleId is { } roleId)
            {
                var guildUser = await gatewayClient.Rest.GetGuildUserAsync(guildId, callerId);
                isCommandStaff = guildUser.RoleIds.Contains(roleId);
            }

            if (!isCommandStaff)
                return "Only Command Staff can terminate another commander's raid alert.";
        }

        var alert = await db.Alerts
            .Include(a => a.Notifications)
            .Where(a => a.GuildId == guildId && a.Type == AlertType.Raid && a.TargetDiscordUserId == targetId && a.TerminatedAt == null)
            .FirstOrDefaultAsync();
        if (alert is null)
            return "No active raid alert found.";

        alert.TerminatedAt = DateTimeOffset.UtcNow;
        alert.TerminatedByDiscordUserId = callerId;
        await db.SaveChangesAsync();

        foreach (var notification in alert.Notifications.Where(n => n.Kind == NotificationKind.Public && n.MessageId is not null))
        {
            await dispatcher.EditPublicAsync(notification.ChannelId, notification.MessageId!.Value, "Raid alert ended.");
        }

        return "Raid alert ended.";
    }

    public async Task<string> SetShieldReminderAsync(ulong guildId, ulong userId, string duration, string system)
    {
        var parsed = DurationParser.Parse(duration);
        if (parsed is null)
            return "Couldn't parse that duration. Use a format like \"2d3h45m\" or \"90m\".";

        var stfcSystem = await FindSystemByNameAsync(system);
        if (stfcSystem is null)
            return $"Unbekanntes System \"{system}\". Bitte die Schreibweise prüfen.";

        var now = DateTimeOffset.UtcNow;

        if (await db.DiscordUsers.FindAsync(userId) is null)
            db.DiscordUsers.Add(new DiscordUser { DiscordUserId = userId });
        if (await db.GuildMembers.FindAsync(guildId, userId) is null)
            db.GuildMembers.Add(new GuildMember { GuildId = guildId, DiscordUserId = userId, JoinedAt = now });

        var reminder = await db.ShieldReminders
            .Include(s => s.Notifications)
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.DiscordUserId == userId);
        if (reminder is null)
        {
            reminder = new ShieldReminder { GuildId = guildId, DiscordUserId = userId };
            db.ShieldReminders.Add(reminder);
        }
        else
        {
            db.ShieldReminderNotifications.RemoveRange(reminder.Notifications);
        }

        reminder.ShieldExpiration = now.Add(parsed.Value);
        reminder.StfcSystemId = stfcSystem.Id;
        reminder.Disabled = false;

        await db.SaveChangesAsync();

        return $"Shield reminder set for <t:{reminder.ShieldExpiration.ToUnixTimeSeconds()}:f> (<t:{reminder.ShieldExpiration.ToUnixTimeSeconds()}:R>) in {stfcSystem.Name}.";
    }

    public async Task<string> TerminateShieldReminderAsync(ulong guildId, ulong userId)
    {
        var reminder = await db.ShieldReminders.FirstOrDefaultAsync(s => s.GuildId == guildId && s.DiscordUserId == userId);
        if (reminder is null)
            return "You don't have a shield reminder set.";

        db.ShieldReminders.Remove(reminder);
        await db.SaveChangesAsync();

        return "Shield reminder removed.";
    }

    // "Alarme verwalten" — legacy just adds/removes a single role (GuildSettings.AlertsRoleId)
    // on the caller, no persistence beyond that. Null return means the role isn't configured.
    public async Task<bool?> HasAlertsRoleAsync(ulong guildId, ulong userId)
    {
        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings?.AlertsRoleId is not { } roleId)
            return null;

        var guildUser = await gatewayClient.Rest.GetGuildUserAsync(guildId, userId);
        return guildUser.RoleIds.Contains(roleId);
    }

    public async Task<string> SetAlertsOptInAsync(ulong guildId, ulong userId, bool optIn)
    {
        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings?.AlertsRoleId is not { } roleId)
            return "Die Alarme-Rolle ist noch nicht konfiguriert (siehe Guild-Einstellungen).";

        try
        {
            if (optIn)
                await gatewayClient.Rest.AddGuildUserRoleAsync(guildId, userId, roleId);
            else
                await gatewayClient.Rest.RemoveGuildUserRoleAsync(guildId, userId, roleId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, "die Alarme-Rolle anpassen", "fehlende Berechtigung (Rolle verwalten)?");
            return "Die Alarme konnten nicht angepasst werden — ein Admin wurde informiert.";
        }

        return optIn
            ? "Commander, die Alarme wurden aktiviert. Danke für Deine Unterstützung!"
            : "Commander, die Alarme wurden deaktiviert. Du kannst sie jederzeit wieder einschalten, um die Allianz zu unterstützen!";
    }
}
