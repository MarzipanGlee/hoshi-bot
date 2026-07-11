namespace HoshiBot.Domain.Entities;

// One value per independently-toggleable feature — roughly one per Command Bridge hub
// button/flow, since that's the granularity a guild admin actually thinks in. Not every
// guild wants every feature; see GuildDisabledFeature for how a feature is turned off.
public enum GuildFeature
{
    RaidAlerts,
    ShieldReminders,
    TerritoryCapture,
    Announcements,
    Tickets,
    AnonymousMessaging,
    RoeViolationReports,
    Absences,
    AlertsOptIn,
    Diplomacy,
    ServerStatus,
    Incursion,
    RankRoles,
}
