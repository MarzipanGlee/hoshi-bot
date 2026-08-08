namespace HoshiBot.Domain.Entities;

// Where a configured channel id can live. The bot has accumulated five storage shapes for "a
// channel this feature points at", and the audit used to hand-write a query plus an inline
// permission switch for each — which is how the announcement forwarder's read-only source channels
// ended up being audited as post targets.
public enum ChannelSlotSource
{
    /// GuildFeatureChannel rows — a list of channels per (feature, audience).
    FeatureChannelList,

    /// GuildFeatureSettingSnowflake rows, keyed by a *SettingKeys constant.
    Setting,

    /// GuildAlertChannel rows, keyed by GuildAlertChannelKind.
    AlertChannel,

    /// A typed channel column on GuildAlliance (the Command Bridge hubs).
    AllianceColumn,

    /// A typed channel column on GuildSettings (log / admin).
    GuildChannel,
}

/// The Command Bridge hub columns on GuildAlliance. Only the three the bot actually posts to —
/// the other seven columns on that entity are admin-UI leftovers with no consumer (see docs/backlog.md).
public enum AllianceChannelColumn { CommandBridge, StaffCommandBridge, FriendsCommandBridge }

/// The GuildSettings channel columns the bot posts to. UserLogChannelId is deliberately absent —
/// nothing reads it (see docs/backlog.md).
public enum GuildChannelColumn { Log, Admin }

// One channel the bot needs access to, and what it does there. Key's meaning depends on Source, and
// it is a plain string rather than a discriminated union so a slot's label can reuse the existing
// (GuildFeature, settingKey) catalog convention (Msg.WebEditor.*) instead of inventing a second
// lookup shape. The typed factories below are what stops a call site typo'ing the discriminator.
public readonly record struct FeatureChannelSlot(
    ChannelSlotSource Source,
    string Key,
    ChannelAccessProfile Profile,
    bool CategoryExpands = false)
{
    // categoryExpands: a configured entry may be a whole category, and the bot works on its child
    // channels rather than the category node — so the audit must check the children. Declared per
    // slot rather than inferred from the profile, so a future read-only slot on a normal channel
    // isn't expanded by accident.
    public static FeatureChannelSlot ChannelList(ChannelAccessProfile profile, bool categoryExpands = false) =>
        new(ChannelSlotSource.FeatureChannelList, "", profile, categoryExpands);

    public static FeatureChannelSlot Setting(string key, ChannelAccessProfile profile) =>
        new(ChannelSlotSource.Setting, key, profile);

    public static FeatureChannelSlot Alert(GuildAlertChannelKind kind, ChannelAccessProfile profile) =>
        new(ChannelSlotSource.AlertChannel, kind.ToString(), profile);

    public static FeatureChannelSlot Alliance(AllianceChannelColumn column, ChannelAccessProfile profile) =>
        new(ChannelSlotSource.AllianceColumn, column.ToString(), profile);

    public static FeatureChannelSlot Guild(GuildChannelColumn column, ChannelAccessProfile profile) =>
        new(ChannelSlotSource.GuildChannel, column.ToString(), profile);
}

// Single source of truth for the Discord permissions the bot needs, per feature — a sibling of
// GuildFeatureAudiences and GuildFeatureDependencies, same Domain home for the same reason: both
// HoshiBot.Web (the permission audit, the invite link) and HoshiBot.Discord (the runtime "I could
// not do X" reports) consult it without a project-reference cycle.
//
// It replaces three declarations that never agreed with each other: a hand-maintained invite
// bitmask, five private constants plus two inline switches in PermissionAuditService, and ~16
// free-text "missing permission in {channel}?" hint strings written at each catch block.
//
// Both switches are non-exhaustive with a safe default, like their siblings — a feature that needs
// nothing simply doesn't appear.
public static class GuildFeaturePermissions
{
    // Every channel this feature is configured with, and what the bot does in each. A feature that
    // posts nowhere (the role-sync features, PlayerLink, NicknameSync, …) has no slots at all — its
    // requirements are entirely guild-level, see GuildPermissions below.
    public static IReadOnlyList<FeatureChannelSlot> ChannelSlots(GuildFeature feature) => feature switch
    {
        // Published announcements ping the warnings role (High) or the alliance notification role
        // (Elevated); the draft channel is read back and decorated with the severity reactions that
        // are now the only way to publish.
        GuildFeature.Announcements =>
        [
            FeatureChannelSlot.Setting(AnnouncementsSettingKeys.Channel, ChannelAccessProfile.PostAndPing),
            FeatureChannelSlot.Setting(AnnouncementsSettingKeys.DraftChannel, ChannelAccessProfile.Draft),
            // Same profile as the real channel: a dry run that can't ping isn't a dry run.
            FeatureChannelSlot.Setting(AnnouncementsSettingKeys.TestChannel, ChannelAccessProfile.PostAndPing),
        ],

        // Two opposite profiles in one feature, which is the clearest proof that the unit here has
        // to be the slot and not the feature: the GuildFeatureChannel rows are *sources* the bot
        // only ever reads, and the Channel setting is the destination it posts translations to.
        GuildFeature.AnnouncementForwarder =>
        [
            FeatureChannelSlot.ChannelList(ChannelAccessProfile.Read),
            FeatureChannelSlot.Setting(AnnouncementForwarderSettingKeys.Channel, ChannelAccessProfile.Post),
        ],

        // Listen channels: reads recent history for context, answers in plain text.
        GuildFeature.AiChat => [FeatureChannelSlot.ChannelList(ChannelAccessProfile.Reply)],

        // The three knowledge tiers are storage-only pseudo-features (no hub button, no
        // IFeatureModule, never in GuildEnabledFeature — they inherit AiChat's enablement, see
        // DisplayOwner). An entry may be a whole category, whose child text/forum channels are what
        // actually gets indexed.
        GuildFeature.AiChatKnowledge
            or GuildFeature.AiChatKnowledgePreferred
            or GuildFeature.AiChatKnowledgeLastResort =>
            [FeatureChannelSlot.ChannelList(ChannelAccessProfile.Read, categoryExpands: true)],

        // Persistent report messages the bot edits in place, re-posting if the message is gone.
        GuildFeature.Absences =>
        [
            FeatureChannelSlot.Setting(AbsencesSettingKeys.ReportChannel, ChannelAccessProfile.Post),
            FeatureChannelSlot.Setting(AbsencesSettingKeys.ReportStaffChannel, ChannelAccessProfile.Post),
        ],

        // Opens one private thread per ticket and talks inside it — never posts in the channel.
        GuildFeature.Tickets => [FeatureChannelSlot.Setting(TicketsSettingKeys.Channel, ChannelAccessProfile.PrivateThreads)],

        // A forum: each report is a forum post, with the diplomat role pinged inside the thread.
        // See ForumPosts — creating the post is SendMessages, not CreatePublicThreads.
        GuildFeature.RoeViolationReports => [FeatureChannelSlot.Setting(RoeViolationReportsSettingKeys.Channel, ChannelAccessProfile.ForumPosts)],

        GuildFeature.AnonymousMessaging => [FeatureChannelSlot.Setting(AnonymousMessagingSettingKeys.Channel, ChannelAccessProfile.Post)],

        // Configured but not yet consumed by anything in HoshiBot.Discord — Diplomacy's channel is
        // a planned feature, so the slot is declared now rather than added later and forgotten.
        GuildFeature.Diplomacy => [FeatureChannelSlot.Setting(DiplomacySettingKeys.Channel, ChannelAccessProfile.Post)],

        // The weekly digest is pinned and pings the zone-slot roles; the pre-capture reminders go to
        // the same channel.
        GuildFeature.TerritoryCapture => [FeatureChannelSlot.Setting(TerritoryCaptureSettingKeys.DigestChannel, ChannelAccessProfile.PostAndPing)],

        GuildFeature.TerritoryCaptureServiceReminders =>
            [FeatureChannelSlot.Setting(TerritoryCaptureServiceRemindersSettingKeys.ServicesChannel, ChannelAccessProfile.PostAndPing)],

        // Alert-channel rows: every one of these is posted through NotificationDispatcher, which
        // prefixes the row's own role mention.
        GuildFeature.RaidAlerts => [FeatureChannelSlot.Alert(GuildAlertChannelKind.Raid, ChannelAccessProfile.PostAndPing)],
        GuildFeature.ShieldReminders => [FeatureChannelSlot.Alert(GuildAlertChannelKind.Shield, ChannelAccessProfile.PostAndPing)],
        GuildFeature.ServerStatus => [FeatureChannelSlot.Alert(GuildAlertChannelKind.ServerStatus, ChannelAccessProfile.PostAndPing)],
        GuildFeature.InfiniteIncursions => [FeatureChannelSlot.Alert(GuildAlertChannelKind.InfiniteIncursions, ChannelAccessProfile.PostAndPing)],
        GuildFeature.AllianceTournament => [FeatureChannelSlot.Alert(GuildAlertChannelKind.AllianceTournament, ChannelAccessProfile.PostAndPing)],

        // Per-platform release notifications ping the platform's opt-in role.
        GuildFeature.ClientRelease => [FeatureChannelSlot.ChannelList(ChannelAccessProfile.PostAndPing)],

        // The hub messages, one per linked alliance per bridge kind. Posted once then edited in
        // place; the buttons on them carry no pings.
        GuildFeature.CommandBridge =>
        [
            FeatureChannelSlot.Alliance(AllianceChannelColumn.CommandBridge, ChannelAccessProfile.Post),
            FeatureChannelSlot.Alliance(AllianceChannelColumn.StaffCommandBridge, ChannelAccessProfile.Post),
            FeatureChannelSlot.Alliance(AllianceChannelColumn.FriendsCommandBridge, ChannelAccessProfile.Post),
        ],

        // News digests and their confirmation counters go to the guild-wide admin channel rather
        // than a channel of their own.
        GuildFeature.StfcNews => [FeatureChannelSlot.Guild(GuildChannelColumn.Admin, ChannelAccessProfile.Post)],

        // Everything else is DM-only (MemberLore, MemberOnboarding), role-only (the sync features),
        // or posts wherever it was invoked and so cannot be audited at all (HoshiSay, and AiChat's
        // replies) — see UnauditableFeatures.
        _ => [],
    };

    // Permissions needed guild-wide, independent of any channel. Discord grants these on the bot's
    // role, so they can only come from the invite/re-authorize flow — no per-channel override helps.
    public static BotPermission GuildPermissions(GuildFeature feature) => feature switch
    {
        // Everything that adds or removes a role on a member. All of these additionally need the
        // bot's own role to sit ABOVE the role being assigned, which is hierarchy rather than
        // permission and is reported separately by the audit page's role-standing banner.
        GuildFeature.AllianceTagRoles
            or GuildFeature.ServerTagRoles
            or GuildFeature.RankRoles
            or GuildFeature.OpsLevelRoles
            or GuildFeature.ConditionalRoles
            or GuildFeature.PlayerLink
            or GuildFeature.Absences
            or GuildFeature.TerritoryCapture
            or GuildFeature.ServicesRoleSync
            or GuildFeature.AlertsOptIn
            or GuildFeature.MemberLore => BotPermission.ManageRoles,

        // Renames members to match their linked player. The only feature that needs this, which is
        // why asking every guild for it at invite time was the clearest case of over-granting.
        GuildFeature.NicknameSync => BotPermission.ManageNicknames,

        _ => BotPermission.None,
    };

    // Every declared slot, flattened once, so discovery can go the other way: given a stored row
    // (an alert-channel Kind, a setting Key) find which feature declared it and with what profile.
    // Built from the enum rather than hand-listed so a slot added above can't be forgotten here.
    public static IReadOnlyList<(GuildFeature Feature, FeatureChannelSlot Slot)> AllSlots { get; } =
    [
        .. Enum.GetValues<GuildFeature>()
            .SelectMany(feature => ChannelSlots(feature).Select(slot => (Feature: feature, Slot: slot))),
    ];

    // Which feature a slot should be *displayed* under. The three AI chat knowledge tiers have no
    // IFeatureModule of their own (they are storage-only), so they cannot be their own card and
    // group under AI Chat instead.
    public static GuildFeature DisplayOwner(GuildFeature feature) => feature switch
    {
        GuildFeature.AiChatKnowledge
            or GuildFeature.AiChatKnowledgePreferred
            or GuildFeature.AiChatKnowledgeLastResort => GuildFeature.AiChat,
        _ => feature,
    };

    // Channels that belong to the guild rather than to any one feature: the activity log and the
    // admin channel every feature's failure reports fall back to (NotificationDispatcher).
    public static IReadOnlyList<FeatureChannelSlot> GuildWideSlots { get; } =
    [
        FeatureChannelSlot.Guild(GuildChannelColumn.Log, ChannelAccessProfile.Post),
        FeatureChannelSlot.Guild(GuildChannelColumn.Admin, ChannelAccessProfile.Post),
    ];

    // Features whose targets are wherever the member happened to be — /hoshi-say posts in the
    // channel it was invoked from, AI chat replies where it was triggered. There is no configured
    // channel to audit, so the audit page says so explicitly rather than letting "no findings" read
    // as "fully covered". Their only real coverage is the runtime failure report.
    public static IReadOnlyList<GuildFeature> UnauditableFeatures { get; } =
        [GuildFeature.HoshiSay, GuildFeature.AiChat];

    // The permissions the bot can only ever get from its server-wide role, because a channel
    // overwrite either cannot express them or means something else there:
    //
    //   ManageNicknames  — not a channel overwrite permission at all; Discord doesn't offer it.
    //   ManageRoles      — on a channel it governs THAT channel's overwrites; assigning a role to a
    //                      member is the server-wide meaning, and that's what the role-sync
    //                      features do.
    //   ManageChannels   — on a channel it governs editing that channel; CREATING a category or
    //                      channel (the Setup Wizard) is the server-wide meaning.
    //
    // Everything else the bot needs is channel-grantable: an overwrite can grant a permission the
    // role does not hold guild-wide, so a cautious admin can decline it server-wide and grant it
    // per channel instead. That distinction is what splits the permission page's re-authorize into
    // a required and an optional half.
    public static BotPermission ServerOnly { get; } =
        BotPermission.ManageRoles | BotPermission.ManageNicknames | BotPermission.ManageChannels;

    // What /invite asks a brand-new guild for, before any feature exists. Three bits.
    //
    // The reason it can be this small is that a member's effective permissions are the OR of
    // @everyone's and their roles' (see DiscordGuildDataService.GetBotRoleStatusAsync), and View
    // Channel, Send Messages, Embed Links and Read Message History are all @everyone defaults — so
    // in any normal guild the bot already holds them without the invite asking. A baseline only has
    // to carry what @everyone won't have.
    //
    // What's left is the two the Setup Wizard needs on first run, before there is anything else:
    // ManageChannels (it creates a category and two channels) and ManageRoles (two roles). Manage
    // Roles is doubly load-bearing — the permission audit's Fix button writes channel overwrites,
    // which needs it. View Channel stays because it is the one @everyone default that guilds
    // routinely deny at category level, and without it the bot cannot even see what it manages.
    //
    // Everything feature-conditional arrives later via the per-guild re-authorize link
    // (BotInviteService): Add Reactions, Mention Everyone, Manage Messages, Manage Nicknames and
    // the three thread bits.
    //
    // The one case this leaves thin: a locked-down guild that denies @everyone Send Messages. The
    // Fix button cannot repair that either — it refuses to grant permissions the bot does not hold
    // itself (EvaluateBotAccessFixability) — so re-authorizing is the only route, which is why that
    // link has to stay easy to find.
    //
    // Deliberately NOT here: ManageMessages. Clearing the clicked severity reaction on an
    // announcement draft would need it (see AnnouncementDraftService), and a guild-wide "delete
    // anyone's messages" grant is far too much to ask for that — an admin who wants it can grant it
    // on the draft channel alone.
    public static BotPermission InviteBaseline { get; } =
        BotPermission.ViewChannel | BotPermission.ManageChannels | BotPermission.ManageRoles;
}
