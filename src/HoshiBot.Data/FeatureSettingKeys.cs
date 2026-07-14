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
