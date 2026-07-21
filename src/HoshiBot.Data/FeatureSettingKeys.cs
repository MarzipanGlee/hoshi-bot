using HoshiBot.Domain.Entities;

namespace HoshiBot.Data;

// Key string constants for GuildFeatureSettingsService, shared between the Web editor that
// writes a setting and the Discord-side code that reads it — only defined here for
// features with a real Discord consumer (Diplomacy/RaidAlerts/ShieldReminders' simple
// fields have none yet, so their editors keep their key strings local).
public static class AbsencesSettingKeys
{
    public const string ReportChannel = "ReportChannel";
    public const string ReportStaffChannel = "ReportStaffChannel";

    // The per-alliance "notify me" ping role, kept absence-clean by NotificationRoleSyncJob
    // (a member with an active SuppressNotifications absence is removed until it ends). Owned
    // by the Absences feature (which manages the sync) but consumed by other features that ping
    // the alliance: the Territory Capture weekly digest and Announcements (Elevated severity).
    // Replaces the old guild-wide NotificationRole table + /set-notification-role command.
    public const string NotificationRole = "NotificationRole";

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
    public const string RemindersChannel = "RemindersChannel";
    public const string DraftChannel = "DraftChannel";

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

public static class AlertsOptInSettingKeys
{
    public const string Role = "Role";
}

public static class ClientReleaseSettingKeys
{
    // One opt-in role per game-client platform, pinged only when THAT platform releases a new
    // version. Guild-wide (client news isn't per-alliance): stored at the None/null scope, so the
    // same four roles show on every audience tab and the opt-in wizard reads them without an
    // alliance. Linux has no version-check source, so it has no role. Members opt in/out via the
    // alerts hub button, alongside the AlertsOptIn role.
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
    public const string DiplomatRole = "DiplomatRole";
}

public static class TerritoryCaptureSettingKeys
{
    // Where this alliance's capture digests are posted — moved off GuildSettings.RemindersChannelId
    // so it lives with the feature (and each alliance can post to its own channel).
    public const string DigestChannel = "DigestChannel";

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

public static class MemberLoreSettingKeys
{
    // The role a user must hold to be DM-interviewed (snowflake). Unset → fall back to the linked
    // alliance's GuildAlliance.MemberRoleId.
    public const string MemberRole = "MemberRole";

    // Max interview invites the bot sends per day (text int, e.g. "10"), to stay clear of Discord's
    // DM rate/anti-spam limits. Unset → a conservative default.
    public const string MaxInterviewsPerDay = "MaxInterviewsPerDay";

    // The invite job's go-signal ("true"): staff flip this on (after posting the announcement) to
    // start DMing members. Off/unset → the feature is configured but no DMs go out.
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

public static class NicknameSyncSettingKeys
{
    // NicknameTagMode (text, e.g. "ForeignOnly") controlling the [alliance-tag] prefix. Unset →
    // ForeignOnly.
    public const string AllianceTagMode = "AllianceTagMode";

    // NicknameTagMode (text) controlling the [server] prefix. Unset → ForeignOnly.
    public const string ServerTagMode = "ServerTagMode";

    // Snowflake list of roles whose holders are skipped entirely (never renamed).
    public const string ExcludedRoles = "ExcludedRoles";
}

public static class RankRolesSettingKeys
{
    public const string AdmiralRole = "AdmiralRole";
    public const string CommodoreRole = "CommodoreRole";
    public const string PremierRole = "PremierRole";
    public const string OperativeRole = "OperativeRole";
    public const string AgentRole = "AgentRole";

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
}
