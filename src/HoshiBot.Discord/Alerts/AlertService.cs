using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.Alerts;

// Core raid/shield-reminder logic shared between the slash commands (AlertModule) and
// the Command Bridge button/modal flow (CommandBridgeModule) — both are valid entry
// points to the same operations.
public class AlertService(
    HoshiBotDbContext db,
    NotificationDispatcher dispatcher,
    GatewayClient gatewayClient,
    EmbedBranding embedBranding,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    GuildAllianceService allianceService,
    PlayerLinkService playerLinkService)
{
    // All strings come from the message catalog (Msg.Alert); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    // This also normalized the file's previously mixed German/English messages to the catalog
    // (the English ones now render German like everything else, per plan decision U4).
    private const Language Lang = Language.De;

    // Buttons can be clicked from a DM (no NetCord Guild context there), so the guild ID
    // travels in the custom_id itself rather than relying on Context.Guild.
    public static ButtonProperties RaidTerminateButton(ulong guildId, ulong targetUserId) =>
        new($"raid-terminate:{guildId}:{targetUserId}", Msg.Alert.TerminateButton(Lang), ButtonStyle.Danger);

    public static ButtonProperties ShieldReminderTerminateButton(ulong guildId) =>
        new($"shield-reminder-terminate:{guildId}", Msg.Alert.TerminateButton(Lang), ButtonStyle.Danger);

    // Case-insensitive: EF Core translates ToUpper() to the SQL UPPER() function — a
    // Unicode-aware case fold on Npgsql. Moot today since every synced system name is
    // English/ASCII anyway.
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
            return Msg.Alert.UnknownSystem(Lang, system);

        var active = await db.Alerts.FirstOrDefaultAsync(a =>
            a.GuildId == guildId && a.Type == AlertType.Raid && a.TargetDiscordUserId == targetUserId && a.TerminatedAt == null);
        if (active is not null)
            return Msg.Alert.AlreadyReported(Lang, $"<@{targetUserId}>", $"<@{active.TriggeredByDiscordUserId}>", active.TriggeredAt.ToUnixTimeSeconds());

        // Excludes test runs, so a self-test never shows up in real history.
        var pastRaids = await db.Alerts
            .Include(a => a.StfcSystem)
            .Where(a => a.GuildId == guildId && a.Type == AlertType.Raid && a.TargetDiscordUserId == targetUserId
                && a.TerminatedAt != null && !a.IsTest)
            .OrderByDescending(a => a.TriggeredAt)
            .Take(5)
            .ToListAsync();

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
            new() { Name = Msg.Alert.FieldSystem(Lang), Value = stfcSystem.Name, Inline = true },
            new() { Name = Msg.Alert.FieldTarget(Lang), Value = $"<@{targetUserId}>", Inline = true },
            new() { Name = Msg.Alert.FieldServer(Lang), Value = ServerLocationLabel(serverLocation), Inline = true },
            new() { Name = Msg.Alert.FieldAttacker(Lang), Value = attacker ?? "-", Inline = true },
            new() { Name = Msg.Alert.FieldReported(Lang), Value = Msg.Alert.ReportedByValue(Lang, now.ToUnixTimeSeconds(), $"<@{triggeredByUserId}>"), Inline = true },
        };
        if (pastRaids.Count > 0)
        {
            var lines = pastRaids.Select(a =>
                Msg.Alert.PastRaidLine(Lang, a.TriggeredAt.ToUnixTimeSeconds(), (int)(a.TerminatedAt!.Value - a.TriggeredAt).TotalMinutes, a.StfcSystem?.Name ?? ""));
            fields.Add(new EmbedFieldProperties { Name = Msg.Alert.PastRaidsField(Lang), Value = string.Join('\n', lines) });
        }

        var embed = await embedBranding.BuildBrandedAsync(guildId,
            Msg.Alert.RaidEmbedBody(Lang, $"<@{targetUserId}>"), EmbedBranding.DangerColor, Msg.Alert.RaidTitle(Lang));
        embed.Fields = fields;

        var publicContent = Msg.Alert.RaidPublic(Lang, $"<@{targetUserId}>");

        if (isTest)
        {
            var alertChannel = await db.GuildAlertChannels.FirstOrDefaultAsync(c => c.GuildId == guildId && c.Kind == GuildAlertChannelKind.Raid);
            var hint = alertChannel is not null
                ? Msg.Alert.TestChannelHint(Lang, $"<#{alertChannel.ChannelId}>")
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

        var dmEmbed = await embedBranding.BuildBrandedAsync(guildId,
            Msg.Alert.RaidDm(Lang, stfcSystem.Name), EmbedBranding.DangerColor);
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
            ? Msg.Alert.TestCompleted(Lang)
            : Msg.Alert.RaidReported(Lang, $"<@{targetUserId}>", stfcSystem.Name);
    }

    private static string ServerLocationLabel(RaidServerLocation location) => location switch
    {
        RaidServerLocation.Home => Msg.Alert.ServerHome(Lang),
        RaidServerLocation.Enemy => Msg.Alert.ServerEnemy(Lang),
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
                return Msg.Alert.OnlyStaffTerminate(Lang);
        }

        var alert = await db.Alerts
            .Include(a => a.Notifications)
            .Where(a => a.GuildId == guildId && a.Type == AlertType.Raid && a.TargetDiscordUserId == targetId && a.TerminatedAt == null)
            .FirstOrDefaultAsync();
        if (alert is null)
            return Msg.Alert.NoActiveRaid(Lang);

        alert.TerminatedAt = DateTimeOffset.UtcNow;
        alert.TerminatedByDiscordUserId = callerId;
        await db.SaveChangesAsync();

        foreach (var notification in alert.Notifications.Where(n => n.Kind == NotificationKind.Public && n.MessageId is not null))
        {
            await dispatcher.EditPublicAsync(notification.ChannelId, notification.MessageId!.Value, Msg.Alert.RaidEnded(Lang));
        }

        return Msg.Alert.RaidEnded(Lang);
    }

    // A shield lives on a station, which can only be parked in a system that supports Station Housing
    // (StfcSystem.HasStationHousing). A system can be a perfectly valid name yet have no housing (e.g.
    // Tezera Beta), so "the system exists" isn't enough to accept a shield reminder there.
    public static string NoStationHousingMessage(string systemName) =>
        Msg.Alert.NoStationHousing(Lang, systemName);

    public async Task<string> SetShieldReminderAsync(ulong guildId, ulong userId, string duration, string system)
    {
        var parsed = DurationParser.Parse(duration);
        if (parsed is null)
            return Msg.Alert.BadDuration(Lang);

        var stfcSystem = await FindSystemByNameAsync(system);
        if (stfcSystem is null)
            return Msg.Alert.UnknownSystem(Lang, system);
        if (!stfcSystem.HasStationHousing)
            return NoStationHousingMessage(stfcSystem.Name);

        var now = DateTimeOffset.UtcNow;
        var expiration = now.Add(parsed.Value);
        await UpsertShieldReminderAsync(guildId, userId, stfcSystem.Id, expiration, now);

        return Msg.Alert.ShieldSet(Lang, expiration.ToUnixTimeSeconds(), stfcSystem.Name);
    }

    // Staff-side "Schildverlust melden/Incursions/Gebietsreset" from the Command Bridge
    // Führungsstab: sets a shield reminder for ANOTHER member, with the expiration derived
    // from the reported variant (see ResolveShieldExpirationAsync). The member is then picked
    // up by ShieldWarningJob like any other reminder.
    public async Task<string> ReportStaffShieldLossAsync(ulong guildId, ulong targetUserId, string system, ShieldLossVariant variant)
    {
        var stfcSystem = await FindSystemByNameAsync(system);
        if (stfcSystem is null)
            return Msg.Alert.UnknownSystem(Lang, system);
        if (!stfcSystem.HasStationHousing)
            return NoStationHousingMessage(stfcSystem.Name);

        var now = DateTimeOffset.UtcNow;
        var expiration = await ResolveShieldExpirationAsync(guildId, targetUserId, variant, now);
        await UpsertShieldReminderAsync(guildId, targetUserId, stfcSystem.Id, expiration, now);

        return Msg.Alert.StaffShieldLossSet(Lang, $"<@{targetUserId}>", stfcSystem.Name, expiration.ToUnixTimeSeconds());
    }

    // Current public-warning mute state for a member (false when no reminder row exists yet).
    public async Task<bool> GetShieldMutedAsync(ulong guildId, ulong userId) =>
        await db.ShieldReminders.AsNoTracking()
            .Where(s => s.GuildId == guildId && s.DiscordUserId == userId)
            .Select(s => s.Muted)
            .FirstOrDefaultAsync();

    // Staff toggle of a member's public shield-warning mute. Creates a disabled placeholder
    // row if the member has no reminder yet, so the flag survives until a real report activates
    // it. Notifies the member of the change (matching the legacy bot's wording).
    public async Task<string> SetShieldMutedAsync(ulong guildId, ulong targetUserId, bool muted)
    {
        var now = DateTimeOffset.UtcNow;

        if (await db.DiscordUsers.FindAsync(targetUserId) is null)
            db.DiscordUsers.Add(new DiscordUser { DiscordUserId = targetUserId });
        if (await db.GuildMembers.FindAsync(guildId, targetUserId) is null)
            db.GuildMembers.Add(new GuildMember { GuildId = guildId, DiscordUserId = targetUserId, JoinedAt = now });

        var reminder = await db.ShieldReminders.FirstOrDefaultAsync(s => s.GuildId == guildId && s.DiscordUserId == targetUserId);
        if (reminder is null)
        {
            reminder = new ShieldReminder { GuildId = guildId, DiscordUserId = targetUserId, ShieldExpiration = now, Disabled = true };
            db.ShieldReminders.Add(reminder);
        }

        reminder.Muted = muted;
        await db.SaveChangesAsync();

        var memberNotice = muted ? Msg.Alert.MutedNotice(Lang) : Msg.Alert.UnmutedNotice(Lang);
        await dispatcher.SendDirectMessageAsync(targetUserId, memberNotice);

        return muted
            ? Msg.Alert.MutedResult(Lang, $"<@{targetUserId}>")
            : Msg.Alert.UnmutedResult(Lang, $"<@{targetUserId}>");
    }

    // StfcEventStatus.EventGroup lookup key for Infinite Incursions (matches
    // InfiniteIncursionsNotifyJob's persisted key — do not change the value).
    private const string InfiniteIncursionsEventGroup = "incursions";

    // Manual = now (already down); Territory Reset = today 20:00 UTC (fixed global reset hour);
    // Infinite Incursions = when the event starts in the reported member's region (see below).
    private async Task<DateTimeOffset> ResolveShieldExpirationAsync(ulong guildId, ulong targetUserId, ShieldLossVariant variant, DateTimeOffset now) => variant switch
    {
        ShieldLossVariant.TerritoryReset => TodayAtUtc(now, new TimeOnly(20, 0)),
        ShieldLossVariant.InfiniteIncursions => await ResolveIncursionsExpirationAsync(guildId, targetUserId, now),
        _ => now,
    };

    // The reported member's shield drops when Infinite Incursions starts in THEIR region. Every
    // player is on one server and every server has a region, so resolve the region from the
    // target player (falling back to the guild's primary alliance region if the member has no
    // linked player). Snap to that region's actually-scheduled EventStart (StfcEventStatus); if
    // none is scheduled, fall back to today at the region's default start time (the Infinite
    // Incursions Schedule / IncursionsRegionDefault).
    private async Task<DateTimeOffset> ResolveIncursionsExpirationAsync(ulong guildId, ulong targetUserId, DateTimeOffset now)
    {
        var regionId = await ResolvePlayerRegionIdAsync(guildId, targetUserId)
            ?? await ResolvePrimaryAllianceRegionIdAsync(guildId);
        if (regionId is not { } rid)
            return now;

        var scheduledStart = await db.StfcEventStatuses
            .Where(e => e.EventGroup == InfiniteIncursionsEventGroup && e.RegionId == rid)
            .Select(e => (DateTimeOffset?)e.EventStart)
            .FirstOrDefaultAsync();
        if (scheduledStart is { } start)
            return start;

        var defaultTime = await db.IncursionsRegionDefaults
            .Where(d => d.RegionId == rid)
            .Select(d => (TimeOnly?)d.DefaultStartTimeUtc)
            .FirstOrDefaultAsync();
        return defaultTime is { } time ? TodayAtUtc(now, time) : now;
    }

    // Target member's region via the server of the player representing them in this guild.
    // Null if they have no linked player.
    private async Task<int?> ResolvePlayerRegionIdAsync(ulong guildId, ulong discordUserId)
    {
        var playerId = await playerLinkService.GetGuildPrimaryPlayerIdAsync(guildId, discordUserId);
        return playerId is null
            ? null
            : await db.StfcPlayers.Where(p => p.Id == playerId).Select(p => (int?)p.Server.RegionId).FirstOrDefaultAsync();
    }

    private async Task<int?> ResolvePrimaryAllianceRegionIdAsync(ulong guildId)
    {
        var primaryId = await allianceService.GetPrimaryIdAsync(guildId);
        if (primaryId is null)
            return null;

        return await db.GuildAlliances
            .Where(ga => ga.Id == primaryId)
            .Select(ga => (int?)ga.StfcAlliance.Server.RegionId)
            .FirstOrDefaultAsync();
    }

    private static DateTimeOffset TodayAtUtc(DateTimeOffset now, TimeOnly time) =>
        new(now.UtcDateTime.Date.Add(time.ToTimeSpan()), TimeSpan.Zero);

    private async Task UpsertShieldReminderAsync(ulong guildId, ulong userId, int stfcSystemId, DateTimeOffset expiration, DateTimeOffset now)
    {
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

        reminder.ShieldExpiration = expiration;
        reminder.StfcSystemId = stfcSystemId;
        reminder.Disabled = false;

        await db.SaveChangesAsync();
    }

    public async Task<string> TerminateShieldReminderAsync(ulong guildId, ulong userId)
    {
        var reminder = await db.ShieldReminders.FirstOrDefaultAsync(s => s.GuildId == guildId && s.DiscordUserId == userId);
        if (reminder is null)
            return Msg.Alert.NoShieldReminder(Lang);

        db.ShieldReminders.Remove(reminder);
        await db.SaveChangesAsync();

        return Msg.Alert.ShieldRemoved(Lang);
    }

    // One opt-in role a member can toggle from the alerts hub button.
    public record OptInRoleState(string Key, string Label, ulong RoleId, bool HasRole);

    // The four ClientRelease platform opt-ins (Linux has no role). Custom-id keys are stable
    // (they travel in the alerts-toggle button custom id); labels are member-facing.
    private static readonly (string Key, string Label, StfcClientPlatform Platform)[] ClientPlatformOptIns =
    [
        ("client-windows", "Windows", StfcClientPlatform.Windows),
        ("client-macos", "macOS", StfcClientPlatform.MacOS),
        ("client-android", "Android", StfcClientPlatform.Android),
        ("client-ios", "iOS", StfcClientPlatform.IOS),
    ];

    // Every opt-in role available to the member: the AlertsOptIn "alerts" role (scoped to the
    // member's alliance) plus the four guild-wide ClientRelease platform roles. A role is
    // included only when its feature is enabled and the role configured; HasRole reflects the
    // member's current membership. Order is stable — alerts first, then the platforms.
    public async Task<IReadOnlyList<OptInRoleState>> GetOptInRolesAsync(ulong guildId, ulong userId)
    {
        var configured = new List<(string Key, string Label, ulong RoleId)>();

        if (await featureService.IsEnabledAsync(guildId, GuildFeature.AlertsOptIn))
        {
            var allianceId = (await allianceService.FindByMemberAsync(guildId, userId))?.Id
                ?? await allianceService.GetPrimaryIdAsync(guildId);
            if (allianceId is not null)
            {
                var roleId = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.AlertsOptIn, GuildAudience.Alliance, allianceId, AlertsOptInSettingKeys.Role);
                if (roleId is { } r)
                    configured.Add(("alerts", Msg.Alert.OptInAlertsLabel(Lang), r));
            }
        }

        if (await featureService.IsEnabledAsync(guildId, GuildFeature.ClientRelease))
        {
            foreach (var (key, label, platform) in ClientPlatformOptIns)
            {
                var roleId = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.ClientRelease, GuildAudience.None, null, ClientReleaseSettingKeys.RoleKey(platform)!);
                if (roleId is { } r)
                    configured.Add((key, label, r));
            }
        }

        if (configured.Count == 0)
            return [];

        var guildUser = await gatewayClient.Rest.GetGuildUserAsync(guildId, userId);
        return configured
            .Select(c => new OptInRoleState(c.Key, c.Label, c.RoleId, guildUser.RoleIds.Contains(c.RoleId)))
            .ToList();
    }

    // Flips one opt-in role for the member (adds if absent, removes if present). Returns a status
    // message; an unknown/unconfigured key or a permission failure returns a friendly message
    // rather than throwing.
    public async Task<string> ToggleOptInRoleAsync(ulong guildId, ulong userId, string key)
    {
        var role = (await GetOptInRolesAsync(guildId, userId)).FirstOrDefault(r => r.Key == key);
        if (role is null)
            return Msg.Alert.RoleUnavailable(Lang);

        try
        {
            if (role.HasRole)
                await gatewayClient.Rest.RemoveGuildUserRoleAsync(guildId, userId, role.RoleId);
            else
                await gatewayClient.Rest.AddGuildUserRoleAsync(guildId, userId, role.RoleId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, Msg.Alert.ActionToggleRole(Lang), Msg.Alert.HintManageRoles(Lang));
            return Msg.Alert.RoleToggleFailed(Lang);
        }

        return role.HasRole ? Msg.Alert.RoleDisabled(Lang, role.Label) : Msg.Alert.RoleEnabled(Lang, role.Label);
    }
}
