namespace HoshiBot.Domain.Entities;

// Which features read each of a scope's SHARED settings — the roles that belong to an alliance or
// an audience rather than to any one feature.
//
// Used to hide a card nothing currently reads. A guild evaluating the bot with every feature still
// switched off was being asked to configure a column of roles, and nothing on the page distinguished
// the ones that would do something from the ones that would sit there. Same instinct as the Features
// page's "requires" badges, pointed the other way: there a feature names what it needs, here a
// setting names who needs it.
//
// Hiding is by USAGE, not by value — a configured role whose features are all off still disappears,
// and reappears with its value intact when one is switched on. Nothing is deleted or reset.
//
// Keep this in step when a feature starts or stops reading one of these. A setting missing from here
// simply never hides, which is the safe direction to be wrong in.
public static class SharedSettingUsage
{
    public static readonly IReadOnlyList<GuildFeature> SeniorStaffRole =
    [
        GuildFeature.RoeViolationReports,   // "report on behalf of an own player"
        GuildFeature.RaidAlerts,            // ending another commander's alert
        GuildFeature.StfcNews,              // who counts as a confirmer
        GuildFeature.Announcements,         // the "im Auftrag von" attribution
    ];

    public static readonly IReadOnlyList<GuildFeature> NotificationRole =
    [
        GuildFeature.Absences,              // owns the absence-clean sync
        GuildFeature.Announcements,         // Elevated severity pings it
        GuildFeature.TerritoryCapture,      // the weekly digest pings it
    ];

    public static readonly IReadOnlyList<GuildFeature> MemberRole =
    [
        GuildFeature.MemberLore,            // who gets interview-invited
        GuildFeature.TerritoryCapture,      // zone-slot role sync gates on alliance membership
    ];

    // Diplomacy is listed even though it is settings-only today and reads nothing: its editor shows
    // the read-only Diplomat card, and that card links here. A feature whose editor shows a shared
    // card must appear in that card's list, or the link lands on a page where the picker is hidden.
    public static readonly IReadOnlyList<GuildFeature> DiplomatRole =
    [
        GuildFeature.Diplomacy,
        GuildFeature.RoeViolationReports,   // pinged once a case is marked ready
    ];

    // Empty on purpose, which hides it unconditionally: nothing in the bot reads it. It is ported
    // config waiting for the feature that used it, and an empty list says so in the one place that
    // decides whether a card appears. Deleting the column is the real fix and is deliberately not
    // done here — that drops a guild's stored ids, which is the admin's call.
    public static readonly IReadOnlyList<GuildFeature> BoardingRole = [];
}
