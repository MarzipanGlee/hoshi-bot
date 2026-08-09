using HoshiBot.Domain;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Data;

// The single source of truth for which buttons live on which Command Bridge, shared by the
// Discord hub builder (HoshiBot.Discord) and the Web admin overview (HoshiBot.Web) so the two
// can never drift. Plain data only — no NetCord types — so both assemblies can reference it.
//
// A button's visibility is NOT configured here: it's gated at render time by whether its
// Feature is enabled for the guild (GuildFeatureService.GetDisabledAsync). The bridge a
// button belongs to is fixed in code (per the user's decision), changeable only by
// enabling/disabling the feature per alliance.
public enum CommandBridgeButtonKind
{
    // A plain button whose custom id / label / emoji are used verbatim.
    Plain,

    // The contact-senior-staff button, expanded at render time into one button per
    // configured audience (Tickets / AnonymousMessaging). CustomId/LabelKey here are ignored
    // by the hub builder; the overview treats it as shown when Tickets OR AnonymousMessaging
    // is enabled.
    ContactStaff,
}

// One button on one bridge. LabelKey is a message-catalog key ("Feature.<X>" where the
// button's label matches the feature label, else "BridgeItem.<X>") — consumers render it
// per language via MessageCatalog.Format. Feature is null for buttons not gated by a
// single GuildFeature (e.g. the beta-tests toggle, which only ever affects the caller's
// own role).
public record CommandBridgeButton(
    CommandBridgeKind Bridge,
    int Row,
    string CustomId,
    string LabelKey,
    string Emoji,
    GuildFeature? Feature,
    CommandBridgeButtonKind Kind = CommandBridgeButtonKind.Plain);

public static class CommandBridgeCatalog
{
    // Order within a bridge is (Row, then list order). Contact-staff (ContactStaff kind)
    // custom id/label are placeholders — the hub builder expands it per audience.
    public static readonly IReadOnlyList<CommandBridgeButton> Buttons =
    [
        // ---- User bridge (matches the legacy/current "Kommandobrücke") ----
        new(CommandBridgeKind.User, 0, "roe-violation-report", "Feature.RoeViolationReports", Icons.RoeViolation, GuildFeature.RoeViolationReports),
        new(CommandBridgeKind.User, 0, "shield-reminder-setup", "BridgeItem.ShieldReminderSetup", Icons.Reminder, GuildFeature.ShieldReminders),
        new(CommandBridgeKind.User, 0, "raid-report", "Feature.RaidAlerts", Icons.Alert, GuildFeature.RaidAlerts),
        new(CommandBridgeKind.User, 1, "announcement-show-unread", "BridgeItem.AnnouncementsUnread", Icons.Unread, GuildFeature.ReadReceipts),
        new(CommandBridgeKind.User, 1, "alerts-manage", "Feature.AlertsOptIn", Icons.RemindersOn, GuildFeature.AlertsOptIn),
        new(CommandBridgeKind.User, 1, "absence-manage", "Feature.Absences", Icons.Absence, GuildFeature.Absences),
        new(CommandBridgeKind.User, 2, "contact-senior-staff", "BridgeItem.ContactStaff", Icons.ContactStaff, null, CommandBridgeButtonKind.ContactStaff),
        new(CommandBridgeKind.User, 2, "channel-guide", "BridgeItem.ChannelGuide", Icons.ChannelGuide, GuildFeature.ChannelGuide),
        new(CommandBridgeKind.User, 2, "bot-support", "BridgeItem.BotSupport", Icons.Help, GuildFeature.BotSupport),

        // ---- Staff bridge ("Kommandobrücke Führungsstab") ----
        new(CommandBridgeKind.Staff, 0, "staff-shield-report", "BridgeItem.StaffShieldReport", Icons.Alert, GuildFeature.ShieldReminders),
        new(CommandBridgeKind.Staff, 0, "staff-shield-incursions", "BridgeItem.StaffShieldIncursions", Icons.Alert, GuildFeature.ShieldReminders),
        new(CommandBridgeKind.Staff, 0, "staff-shield-territory-reset", "BridgeItem.StaffShieldTerritoryReset", Icons.Alert, GuildFeature.ShieldReminders),
        new(CommandBridgeKind.Staff, 0, "staff-shield-mute", "BridgeItem.StaffShieldMute", Icons.RemindersOff, GuildFeature.ShieldReminders),
        // Reads the roster gap out of the player links, so it follows Player Assignment's toggle:
        // without those links every player would look missing.
        new(CommandBridgeKind.Staff, 1, "staff-missing-players", "BridgeItem.StaffMissingPlayers", Icons.MissingPlayers, GuildFeature.PlayerLink),

        // ---- Friends bridge ("Kommandobrücke Freunde") — a trimmed user subset ----
        new(CommandBridgeKind.Friends, 0, "shield-reminder-setup", "BridgeItem.ShieldReminderSetup", Icons.Reminder, GuildFeature.ShieldReminders),
        new(CommandBridgeKind.Friends, 0, "raid-report", "Feature.RaidAlerts", Icons.Alert, GuildFeature.RaidAlerts),
        new(CommandBridgeKind.Friends, 0, "alerts-manage", "Feature.AlertsOptIn", Icons.RemindersOn, GuildFeature.AlertsOptIn),
        new(CommandBridgeKind.Friends, 0, "contact-senior-staff", "BridgeItem.ContactStaff", Icons.ContactStaff, null, CommandBridgeButtonKind.ContactStaff),
    ];

    public static IEnumerable<CommandBridgeButton> ForBridge(CommandBridgeKind bridge) =>
        Buttons.Where(b => b.Bridge == bridge);
}
