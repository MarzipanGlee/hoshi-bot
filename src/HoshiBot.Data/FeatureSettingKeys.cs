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
