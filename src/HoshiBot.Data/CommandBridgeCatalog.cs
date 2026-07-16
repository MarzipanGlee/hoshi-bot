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

    // The "Führungsstab kontaktieren" button, expanded at render time into one button per
    // configured audience (Tickets / AnonymousMessaging). CustomId/Label here are ignored by
    // the hub builder; the overview treats it as shown when Tickets OR AnonymousMessaging is
    // enabled.
    ContactStaff,
}

// One button on one bridge. Feature is null for buttons not gated by a single GuildFeature
// (e.g. the beta-tests toggle, which only ever affects the caller's own role).
public record CommandBridgeButton(
    CommandBridgeKind Bridge,
    int Row,
    string CustomId,
    string LabelDe,
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
        new(CommandBridgeKind.User, 0, "roe-violation-report", "ROE Verstoss melden", "🚫", GuildFeature.RoeViolationReports),
        new(CommandBridgeKind.User, 0, "shield-reminder-setup", "Schilderinnerung einrichten", "⏰", GuildFeature.ShieldReminders),
        new(CommandBridgeKind.User, 0, "raid-report", "Raid melden", "🚨", GuildFeature.RaidAlerts),
        new(CommandBridgeKind.User, 1, "announcement-show-unread", "Ungelesene Ankündigungen", "🟩", GuildFeature.Announcements),
        new(CommandBridgeKind.User, 1, "alerts-manage", "Alarme verwalten", "🔔", GuildFeature.AlertsOptIn),
        new(CommandBridgeKind.User, 1, "absence-manage", "Abwesenheiten verwalten", "⛺", GuildFeature.Absences),
        new(CommandBridgeKind.User, 2, "contact-command-staff", "Führungsstab kontaktieren", "📮", null, CommandBridgeButtonKind.ContactStaff),

        // ---- Staff bridge ("Kommandobrücke Führungsstab") ----
        new(CommandBridgeKind.Staff, 0, "staff-shield-report", "Schildverlust melden", "🚨", GuildFeature.ShieldReminders),
        new(CommandBridgeKind.Staff, 0, "staff-shield-incursions", "Schildverlust Incursions", "🚨", GuildFeature.ShieldReminders),
        new(CommandBridgeKind.Staff, 0, "staff-shield-territory-reset", "Schildverlust Gebietsreset", "🚨", GuildFeature.ShieldReminders),
        new(CommandBridgeKind.Staff, 0, "staff-shield-mute", "Öffentliche Schildablaufwarnungen verwalten", "🔕", GuildFeature.ShieldReminders),
        // Beta-tests toggles only the caller's own role — always shown on the staff bridge,
        // not gated by a GuildFeature.
        new(CommandBridgeKind.Staff, 1, "staff-beta-tests", "Beta-Tests verwalten", "👷", null),

        // ---- Friends bridge ("Kommandobrücke Freunde") — a trimmed user subset ----
        new(CommandBridgeKind.Friends, 0, "shield-reminder-setup", "Schilderinnerung einrichten", "⏰", GuildFeature.ShieldReminders),
        new(CommandBridgeKind.Friends, 0, "raid-report", "Raid melden", "🚨", GuildFeature.RaidAlerts),
        new(CommandBridgeKind.Friends, 0, "alerts-manage", "Alarme verwalten", "🔔", GuildFeature.AlertsOptIn),
        new(CommandBridgeKind.Friends, 0, "contact-command-staff", "Führungsstab kontaktieren", "📮", null, CommandBridgeButtonKind.ContactStaff),
    ];

    public static IEnumerable<CommandBridgeButton> ForBridge(CommandBridgeKind bridge) =>
        Buttons.Where(b => b.Bridge == bridge);
}
