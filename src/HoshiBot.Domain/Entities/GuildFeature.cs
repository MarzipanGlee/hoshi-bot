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
    NotificationOptIn,
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

    // Alliance-audience: keeps the Territory Capture Services role in lock-step with the rank roles
    // that hold the in-game "Activate Services" permission (Admiral + Commodore) — every alliance
    // member with either rank role gets the Services role; anyone who no longer qualifies loses it.
    // Depends on RankRoles (source) and TerritoryCapture (owns the Services role). Owns no settings
    // of its own: the sync (TerritoryCaptureRoleSyncJob) and its editor read/write the existing TC
    // ServicesRole + GuildAlliance.MemberRoleId. Keep last so existing enum ordinals/DB rows don't shift.
    ServicesRoleSync,

    // Guild-wide admin utility: the /hoshi-say slash command, which lets an admin have Hoshi compose
    // a message in her own voice (via the AI backend) and post it as a plain chat line into the
    // channel the command is used in. Limited to members holding the configured allowed role (a
    // snowflake setting) — role membership is the permission gate. Depends on AiBackend (the model
    // that composes the text). Keep last so existing enum ordinals/DB rows don't shift.
    HoshiSay,

    // Alliance-audience, settings-free: lets members sign off from an upcoming territory capture.
    // With it on, the TC digests/reminders ask members to sign off and carry the "Abmelden für
    // {zone}" buttons; each click writes an Absence row. Depends on TerritoryCapture (whose posts
    // carry the buttons) and Absences (which owns the rows the buttons create) — it was a
    // TerritoryCaptureSettingKeys.AbsenceSignOff switch first, but a toggle whose real requirement
    // was "another feature must be on" reads far clearer as a feature with declared dependencies.
    // Keep last so existing enum ordinals/DB rows don't shift.
    TerritoryCaptureSignOff,

    // Alliance-audience: the post-capture "activate services" nudge for officers — its own channel
    // + role and the per-zone service curation (TerritoryServiceSelection), split out of
    // TerritoryCapture. Independent of the capture digests in every practical sense (different
    // channel, different audience, an alliance can want one without the other), so it toggles on
    // its own; TerritoryCapture stays a dependency because the capture times it fires from come
    // from that schedule. Keep last so existing enum ordinals/DB rows don't shift.
    TerritoryCaptureServiceReminders,

    // Guild-wide: gives every linked member a role named after their alliance's tag, so a coalition
    // or server-wide Discord shows at a glance who is from where. Sibling of RankRoles/OpsLevelRoles
    // but NOT an exclusive-tier feature in the same sense — the "tiers" are arbitrary alliance tags
    // rather than an enum, so the roles are discovered/created at runtime and tracked per alliance in
    // the settings store (AllianceTagRolesSettingKeys.RoleFor). Keep last so existing enum
    // ordinals/DB rows don't shift.
    AllianceTagRoles,

    // Guild-wide sibling of AllianceTagRoles, one tier down: a role per STFC server the guild counts
    // as its own (GuildServerScope — its linked alliances' servers plus servers it tracks
    // directly), plus one shared role for members from anywhere else.
    // Unlike alliance tags the set is closed and known from configuration, so it rides on
    // ExclusiveTierRoleSyncJob with server ids as tiers rather than discovering roles at runtime.
    // Keep last so existing enum ordinals/DB rows don't shift.
    ServerTagRoles,

    // Guild-wide: admin-authored rules that grant a role while a boolean expression over a member's
    // other roles holds, and take it away when it stops holding. The generic form of what features
    // like ServicesRoleSync hardcode in C# — its trees live in their own tables
    // (ConditionalRoleRule/Condition/Node) rather than the settings store, since a tree needs
    // ordering and referential integrity. Keep last so existing enum ordinals/DB rows don't shift.
    ConditionalRoles,

    // Alliance-audience: a "Help" button on the user Command Bridge that points members at the
    // channel where they can ask about the bot itself. One channel setting and nothing else — the
    // bot never posts there, it only mentions it, which is why the channel carries no permission
    // slot. Ported from legacy's command_bridge/common-ch.yag; the channel it reads used to be
    // GuildAlliance.BotSupportChannelId, migrated into this feature's settings.
    // Keep last so existing enum ordinals/DB rows don't shift.
    BotSupport,

    // Alliance-audience: a "Help with something else" button on the user Command Bridge that shows
    // an admin-authored message pointing at the channels a question actually belongs in. The text is
    // the whole feature — legacy hardcoded one alliance's channel list into the bot
    // (command_bridge/common-ch.yag), which is exactly the thing a setting should own.
    // Keep last so existing enum ordinals/DB rows don't shift.
    ChannelGuide,

    // Guild-wide: which kinds of post carry a "read this" confirmation button, and therefore what
    // the unread list contains. Not a switch on Announcements — the commonest readable post by far
    // is a forwarded translation, and diplomacy posts, the welcome message and the alliance rules
    // are all coming. Each post records the answer at the moment it is made (see
    // ReadablePost.ReadReceiptsEnabled), so changing the setting only ever affects new posts.
    // Keep last so existing enum ordinals/DB rows don't shift.
    ReadReceipts,

    // The welcome message a new member confirms to get in: a standing post an admin writes, carrying
    // the button that grants the member role and takes the boarding role away. Legacy had it and the
    // port left the pieces behind — GuildAlliance.BoardingRoleId, AllianceBoardingChannelId and
    // ReadablePostKind.WelcomeMessage all predate this feature and were waiting for it.
    //
    // Per audience: an alliance boards the members whose linked player is actually in it, while a
    // server/veil-group/community Discord boards everyone who joins. A member is boarded once,
    // by the narrowest scope that claims them.
    // Keep last so existing enum ordinals/DB rows don't shift.
    Boarding,
}
