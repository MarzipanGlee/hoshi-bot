namespace HoshiBot.Domain.Entities;

// Key string constants for GuildFeatureSettingsService, shared between the Web editor that
// writes a setting and the Discord-side code that reads it — only defined here for
// features with a real Discord consumer (Diplomacy/RaidAlerts/ShieldReminders' simple
// fields have none yet, so their editors keep their key strings local).
public static class AbsencesSettingKeys
{
    public const string ReportChannel = "ReportChannel";
    public const string ReportStaffChannel = "ReportStaffChannel";


    // Per-alliance "pinned report message" ids — the message the periodic refresh edits in place
    // in each alliance's report channel. Replaces GuildSettings.AbsencesReport*MessageId (which
    // could only track one message per guild) now that each alliance has its own report channel.
    public const string ReportMessageId = "ReportMessageId";
    public const string ReportStaffMessageId = "ReportStaffMessageId";
}

public static class RoeViolationReportsSettingKeys
{
    public const string Channel = "Channel";
}

public static class AnnouncementsSettingKeys
{
    public const string Channel = "Channel";
    public const string DraftChannel = "DraftChannel";

    // A dry-run destination: with one configured, the publish prompt offers it beside the real
    // channel so staff can see a finished announcement in place before it reaches the guild.
    // Legacy had exactly this as a second hardcoded publish button ("ankündigungen-test").
    public const string TestChannel = "TestChannel";

    // The standing "how to write an announcement" hub posted in the draft channel, so a refresh
    // edits it in place instead of stacking a second copy. A message id rather than a channel, like
    // AbsencesSettingKeys.ReportMessageId — it declares no permission slot for that reason.
    public const string DraftHubMessageId = "DraftHubMessageId";

    // The role pinged on a High-severity announcement — moved off GuildSettings.WarningsRoleId
    // so it lives with the feature (and can differ per alliance).
    public const string WarningsRole = "WarningsRole";
}

public static class TicketsSettingKeys
{
    public const string Channel = "Channel";
}

public static class AnonymousMessagingSettingKeys
{
    public const string Channel = "Channel";
}

public static class ClientReleaseSettingKeys
{
    // One opt-in role per game-client platform, pinged only when THAT platform releases a new
    // version. Guild-wide (client news isn't per-alliance): stored at the None/null scope, so the
    // same four roles show on every audience tab and the opt-in wizard reads them without an
    // alliance. Linux has no version-check source, so it has no role. Members opt in/out via the
    // alerts hub button, alongside the NotificationOptIn role.
    public const string WindowsRole = "WindowsRole";
    public const string MacOSRole = "MacOSRole";
    public const string AndroidRole = "AndroidRole";
    public const string IOSRole = "IOSRole";

    // Null for Linux (no source, no role). Central so the editor, notify job, and opt-in wizard
    // all map a platform to the same key.
    public static string? RoleKey(StfcClientPlatform platform) => platform switch
    {
        StfcClientPlatform.Windows => WindowsRole,
        StfcClientPlatform.MacOS => MacOSRole,
        StfcClientPlatform.Android => AndroidRole,
        StfcClientPlatform.IOS => IOSRole,
        _ => null,
    };
}

public static class DiplomacySettingKeys
{
    public const string Channel = "Channel";
}

public static class TerritoryCaptureSettingKeys
{
    // Where this alliance's capture digests are posted — moved off GuildSettings.RemindersChannelId
    // so it lives with the feature (and each alliance can post to its own channel).
    public const string DigestChannel = "DigestChannel";

    // LOCAL "HH:mm" (:00/:30) digest fire times, interpreted in the alliance's GuildAlliance.TimeZoneId
    // (DST-aware). Unset → the Default* below, which — with the default Europe/Zurich zone — reproduce
    // the previous hard-coded weekly-09:00 / daily-19:00 Europe/Zurich cron exactly. The weekday
    // itself is not configurable — it follows TerritoryCaptureScheduler.WeeklyDigestWeekday.
    public const string WeeklyDigestTime = "WeeklyDigestTime";
    public const string DailyDigestTime = "DailyDigestTime";
    public const string DefaultWeeklyTime = "09:00";
    public const string DefaultDailyTime = "19:00";

    public const string ZoneSlot1Role = "ZoneSlot1Role";
    public const string ZoneSlot2Role = "ZoneSlot2Role";
    public const string ZoneSlot3Role = "ZoneSlot3Role";
    public const string ZoneSlot4Role = "ZoneSlot4Role";
    public const string ZoneSlot5Role = "ZoneSlot5Role";
    public const string Instructions = "Instructions";

    // Mirrors GuildSettings.GetZoneSlotRoleId's old slot-number indexing (1-5), now backed
    // by the generic settings table instead of 5 fixed columns.
    public static string ZoneSlotRole(int slotIndex) => slotIndex switch
    {
        1 => ZoneSlot1Role,
        2 => ZoneSlot2Role,
        3 => ZoneSlot3Role,
        4 => ZoneSlot4Role,
        5 => ZoneSlot5Role,
        _ => throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Zone slot index must be 1-5."),
    };
}

// The post-capture "activate services" nudge. Both keys used to live on TerritoryCaptureSettingKeys
// (and were stored under GuildFeature.TerritoryCapture) — a data migration moved the existing rows
// over when this became its own feature. The per-zone service curation isn't here at all: it's the
// TerritoryServiceSelection table, keyed by alliance/zone/service rather than by feature.
public static class TerritoryCaptureServiceRemindersSettingKeys
{
    // Where the reminder is posted and the officer role it pings. Deliberately its own channel, not
    // the capture DigestChannel — this one is for the handful of officers who can activate services.
    public const string ServicesChannel = "ServicesChannel";
    public const string ServicesRole = "ServicesRole";
}

public static class MemberLoreSettingKeys
{
    // The role a user must hold to be DM-interviewed (snowflake). Unset → fall back to the linked
    // alliance's GuildAlliance.MemberRoleId.
    public const string MemberRole = "MemberRole";

    // Max interview invites the bot sends per day (text int, e.g. "10"), to stay clear of Discord's
    // DM rate/anti-spam limits. Unset → a conservative default.
    public const string MaxInterviewsPerDay = "MaxInterviewsPerDay";

    // The invite job's go-signal ("true"): staff flip this on to start DMing members. Off/unset →
    // the feature is configured but no DMs go out.
    public const string CampaignActive = "CampaignActive";

    // Optional role (snowflake) granted to a member once they finish their interview. Unset → none.
    public const string CompletedRole = "CompletedRole";
}

public static class MemberOnboardingSettingKeys
{
    // The go-signal ("true") for the opt-in DM outreach: staff flip this on to let the bot DM members
    // with an Unresolved PlayerLinkReview row. Off/unset → no DMs, admin-table resolution only.
    public const string CampaignActive = "CampaignActive";

    // Max onboarding DMs the bot sends per day (text int, e.g. "10"), to stay clear of Discord's DM
    // rate/anti-spam limits. Unset → a conservative default.
    public const string MaxInvitesPerDay = "MaxInvitesPerDay";
}

public static class PlayerLinkSettingKeys
{
    // Optional role for members who have no linked player. Unlike every other role sync, which only
    // ever visits members it HAS player data for, this one is defined by their absence — so it walks
    // the whole roster. Unset → nothing is assigned. Removed again the moment a member gets linked.
    public const string UnlinkedRole = "UnlinkedRole";
}

public static class AllianceTagRolesSettingKeys
{
    // Whether the sync may create a role it can't find. Off → it only assigns roles that already
    // exist, so a guild can hand-make exactly the ones it wants. Default-ON (the feature does very
    // little otherwise): "false" is stored when switched OFF, the row is deleted when switched on.
    public const string CreateMissing = "CreateMissing";

    // Rewrite the tag to look-alike ASCII before naming a role (ĶÅØŞ → KAOS). Default off — it only
    // affects how a NEW role is NAMED; matching always accepts both spellings, so turning it on or
    // off never orphans the roles a guild already has. See AllianceTagRoleName.Candidates.
    public const string Latinize = "Latinize";

    // Name new roles in lower case. Same deal: naming only, since assignment is case-insensitive.
    public const string Lowercase = "Lowercase";

    // Optional strings wrapped around the tag, e.g. prefix "[" + suffix "]" → "[KAOS]". Also the
    // safest way to keep tag roles visually distinct from unrelated roles that happen to share a
    // short name.
    public const string RolePrefix = "RolePrefix";
    public const string RoleSuffix = "RoleSuffix";

    // Optional role for a linked member whose player currently has no alliance. Unset → those
    // members simply hold no tag role.
    public const string NoAllianceRole = "NoAllianceRole";

    // Optional single role for members whose alliance lives on a server this guild doesn't track.
    // Tag roles are only created for the guild's own server(s) — otherwise a Discord with visitors
    // from all over would accumulate a role per foreign alliance and run into Discord's 250-role
    // cap. Unset → those members hold no tag role at all.
    public const string ForeignAllianceRole = "ForeignAllianceRole";

    // The role bound to one alliance, written by the sync as it adopts or creates roles. Keyed by
    // StfcAlliance.Id rather than by tag because StfcAllianceImportService rewrites Tag when an
    // alliance renames itself — the binding has to survive that. Enumerating these rows is also how
    // the sync knows which roles are its own, and therefore which stale ones to remove.
    public static string RoleFor(int stfcAllianceId) => $"{RolePrefixKey}{stfcAllianceId}";

    public const string RolePrefixKey = "Role:";
}

public static class ServerTagRolesSettingKeys
{
    // The role for one server, picked (or created) in the editor rather than discovered by the sync
    // — unlike alliance tags, the server set is closed and known from the guild's own configuration
    // (GuildServerScope), so there is a card for each one up front. Keyed by StfcServer.Id, which is
    // the real Scopely server number and never changes.
    public static string RoleFor(int stfcServerId) => $"{RolePrefixKey}{stfcServerId}";

    public const string RolePrefixKey = "Role:";

    // Optional single role for members playing on a server this guild doesn't count as its own.
    // Unset → those members hold no server role at all.
    public const string ForeignServerRole = "ForeignServerRole";
}

public static class NicknameSyncSettingKeys
{
    // NicknameTagMode (text, e.g. "ForeignOnly") controlling the [alliance-tag] prefix. Unset →
    // ForeignOnly.
    public const string AllianceTagMode = "AllianceTagMode";

    // NicknameTagMode (text) controlling the [server] prefix. Unset → ForeignOnly.
    public const string ServerTagMode = "ServerTagMode";

    // Snowflake list of roles whose holders are skipped entirely (never renamed).
    public const string ExcludedRoles = "ExcludedRoles";

    // Whether a member's own suffix (DiscordUser.NicknameSuffix, set on /me) is appended in
    // parentheses — "[TAG] Player (Suffix)". Default-ON, so "false" is stored when switched
    // OFF and the row is deleted when switched back on; a guild that doesn't want member-authored
    // text in its nicknames turns it off without members losing the suffix in their other guilds.
    public const string MemberSuffix = "MemberSuffix";

    // Unset (or anything but an explicit "false") → on. Shared by the Web editor and the sync job so
    // the default can't drift between what admins see and what actually gets written.
    public static bool IsMemberSuffixOn(string? storedValue) =>
        !string.Equals(storedValue, "false", StringComparison.OrdinalIgnoreCase);
}

public static class RankRolesSettingKeys
{
    public const string AdmiralRole = "AdmiralRole";
    public const string CommodoreRole = "CommodoreRole";
    public const string PremierRole = "PremierRole";
    public const string OperativeRole = "OperativeRole";
    public const string AgentRole = "AgentRole";

    // Optional role for a member who currently has no rank — which, since a rank only exists inside
    // an alliance, is the same thing as having no alliance. Unset → those members just hold none of
    // the five rank roles.
    public const string NoRankRole = "NoRankRole";

    // Lets the sync job go straight from a player's Rank to the one key that applies,
    // instead of a switch at every call site.
    public static string RoleForRank(StfcPlayerRank rank) => rank switch
    {
        StfcPlayerRank.Admiral => AdmiralRole,
        StfcPlayerRank.Commodore => CommodoreRole,
        StfcPlayerRank.Premier => PremierRole,
        StfcPlayerRank.Operative => OperativeRole,
        StfcPlayerRank.Agent => AgentRole,
        _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, "Unknown STFC rank."),
    };
}

public static class OpsLevelRolesSettingKeys
{
    public const string G1Role = "G1Role";
    public const string G2Role = "G2Role";
    public const string G3Role = "G3Role";
    public const string G4Role = "G4Role";
    public const string G5Role = "G5Role";
    public const string G6Role = "G6Role";
    public const string G7Role = "G7Role";

    // Lets the sync job go straight from a player's derived Ops group to the one key that
    // applies, instead of a switch at every call site.
    public static string RoleForGroup(StfcOpsGroup group) => group switch
    {
        StfcOpsGroup.G1 => G1Role,
        StfcOpsGroup.G2 => G2Role,
        StfcOpsGroup.G3 => G3Role,
        StfcOpsGroup.G4 => G4Role,
        StfcOpsGroup.G5 => G5Role,
        StfcOpsGroup.G6 => G6Role,
        StfcOpsGroup.G7 => G7Role,
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown Ops group."),
    };
}

public static class AnnouncementForwarderSettingKeys
{
    // Where the translated announcement is posted — same key string as AnnouncementsSettingKeys.Channel,
    // just under this feature, so it follows the same "Channel" convention used across simple
    // single-destination features (RoeViolationReports, Announcements).
    public const string Channel = "Channel";

    // Optional override for the language to translate into (an FtsLanguage-style config name); unset
    // falls back to the guild's Discord preferred locale.
    public const string TargetLanguage = "TargetLanguage";

    // How far back (in hours) AnnouncementForwarderCatchUpJob will ever look for a missed
    // announcement. Unset -> DefaultCatchUpWindowHours. Time-bounded rather than count-bounded: these
    // source channels post rarely, so a fixed message count would span months and risk resurrecting
    // stale news on first enable or after a long outage.
    public const string CatchUpWindowHours = "CatchUpWindowHours";
    public const int DefaultCatchUpWindowHours = 96;
}

public static class HoshiSaySettingKeys
{
    // The role permitted to run /hoshi-say. Membership in this role is the command's permission gate;
    // the command posts into whatever channel it was invoked from. Unset -> the command is unusable
    // (nobody qualifies), so the editor requires setting it.
    public const string AllowedRole = "AllowedRole";
}

public static class BotSupportSettingKeys
{
    // The channel members are pointed at for help with the bot itself. Only ever rendered as a
    // "<#id>" mention — the bot never posts here — so it deliberately declares no channel permission
    // slot in GuildFeaturePermissions. Was GuildAlliance.BotSupportChannelId before the feature
    // existed; MoveBotSupportChannelToFeature carries those values over.
    public const string Channel = "Channel";
}

public static class ChannelGuideSettingKeys
{
    // The whole message the "Help with something else" button shows, authored by the admins. Free
    // text so it can carry "<#id>" channel mentions and markdown; "{commander}" is substituted with
    // the clicking member's name. Unset -> the button says so rather than showing an empty embed.
    public const string Message = "Message";
}

public static class ReadReceiptsSettingKeys
{
    // One "true"/"false" switch per post kind, keyed by the enum member name — the same
    // parameterised-key shape as TerritoryCaptureSettingKeys.ZoneSlotRole(int). Unset means off, so
    // a newly declared kind starts silent rather than retroactively demanding confirmations.
    public static string ForKind(ReadablePostKind kind) => $"Kind.{kind}";
}
