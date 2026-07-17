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
    InfiniteIncursions,
    RankRoles,
    OpsLevelRoles,
    StfcNews,
    AllianceTournament,
    ClientRelease,
    AiChat,

    // Storage-only sibling of AiChat: a second GuildFeatureChannel bucket holding the
    // "knowledge source" channels the AI reads to ground its answers. It is deliberately NOT a
    // real toggleable feature — it has no Command Bridge hub button, no Web IFeatureModule, and
    // is never enabled via GuildEnabledFeature; it only ever appears as a (GuildId, Feature,
    // Audience) key on GuildFeatureChannel rows so the same MultiChannelPicker/service can back
    // the knowledge-channel list. Keep it last so its ordinal never shifts existing rows.
    AiChatKnowledge,
}
