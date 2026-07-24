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
    // AiChatKnowledge itself is the "Normal" priority tier for knowledge retrieval.
    AiChatKnowledge,

    // Two more storage-only knowledge buckets (same shape/rules as AiChatKnowledge) expressing a
    // per-channel retrieval PRIORITY: Preferred sources are boosted and LastResort sources are
    // down-ranked (used only when better sources have no answer) during AI-chat search. The
    // indexed knowledge set is the union of all three buckets. Keep these last so ordinals never
    // shift existing GuildFeatureChannel rows.
    AiChatKnowledgePreferred,
    AiChatKnowledgeLastResort,

    // Member lore: the bot DM-interviews members to learn who they are (name, interests, stories
    // about others) so it can act like a real member of the community. See docs/ai-chat-member-lore.md.
    MemberLore,

    // Automated player↔member assignment: matches a member's (tag-stripped) Discord nickname against
    // the alliance roster and creates the UserPlayer link that drives every role-sync job. Confident
    // matches link silently; anything ambiguous becomes an admin-resolved PlayerLinkReview row. Never
    // messages members. Keep after MemberLore so existing enum ordinals/DB rows don't shift.
    PlayerLink,

    // Opt-in, member-facing companion to PlayerLink: DMs members with an Unresolved PlayerLinkReview
    // row to confirm/pick their in-game player. Off by default — a guild that doesn't want the bot
    // DMing members leaves it off and resolves assignments via PlayerLink's admin table only.
    MemberOnboarding,

    // Guild-wide: renames members' Discord nicknames to match their main linked player's in-game name,
    // optionally prefixed with server/alliance tags (see NicknameSyncSettingKeys). Sibling of
    // Rank/Ops Level Roles — a single Guild-audience toggle driven by the player links.
    NicknameSync,

    // The per-alliance "Kommandobrücke" hub messages (User/Staff/Friends bridges) — the member-
    // and staff-facing entry point most other features' buttons live on. Alliance-audience; its
    // channels + posted-message ids are typed columns on GuildAlliance (not the settings store).
    // Every feature that contributes a bridge button declares this as a dependency
    // (GuildFeatureDependencies). Keep last so existing enum ordinals/DB rows don't shift.
    CommandBridge,

    // Auto-translates crossposted official announcements (e.g. Scopely's English STFC news) posted in
    // the configured source channels and reposts a branded translation into a destination channel, so
    // members who don't read the source language still see them. Guild-wide, like AiChat.
    AnnouncementForwarder,

    // Guild-wide AI backend configuration: the LLM provider, API key, and model choices (chat, gate,
    // router, member-lore, embeddings) shared by every AI-powered feature. One AI account per guild,
    // so this is a single Guild-audience toggle — AiChat, MemberLore and AnnouncementForwarder all
    // depend on it. Split out of AiChat (where these scalars used to live guild-wide at the None
    // scope) so the per-audience AiChat feature no longer carries guild-wide credentials. Keep last
    // so existing enum ordinals/DB rows don't shift.
    AiBackend,
}
