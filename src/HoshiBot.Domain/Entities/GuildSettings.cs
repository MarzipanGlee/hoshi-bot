namespace HoshiBot.Domain.Entities;

// One row per guild — the channel/role IDs configuring features not built yet (Alerts,
// Shield Reminders, Announcements, Command Bridge/Diplomacy, Rules, RoE violations). Ported
// from hoshi-bot-yagpdb's hardcoded, single-guild definitions-snowflakes.yag; all nullable
// since a guild may not have every feature's channel/role set up. Alert channel/role pairs
// are the one genuinely multi-value setting and live in their own child table — see
// GuildAlertChannel. Territory Capture zone-slot roles used to be a child table too
// (GuildTerritoryCaptureZoneSlotRole), but an alliance always has exactly 5 zone slots, so
// that's collapsed into 5 fixed columns here instead, same as the 5 STFC rank roles below.
public class GuildSettings
{
    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    // Channels
    public ulong? LogChannelId { get; set; }
    public ulong? AdminChannelId { get; set; }
    public ulong? UserLogChannelId { get; set; }
    public ulong? AbsencesReportChannelId { get; set; }
    public ulong? AbsencesReportStaffChannelId { get; set; }
    // The two persistent, edited-in-place "Abwesenheiten" report messages — tracked so
    // AbsenceService.RefreshReportsAsync can edit them in place instead of re-posting,
    // same role CommandBridgeMessageId plays for the hub message.
    public ulong? AbsencesReportMessageId { get; set; }
    public ulong? AbsencesReportStaffMessageId { get; set; }
    public ulong? AllianceBoardingChannelId { get; set; }
    public ulong? AnnouncementsChannelId { get; set; }
    public ulong? AnnouncementsRemindersChannelId { get; set; }
    // Where staff post announcement drafts (plain message, attachments allowed) before
    // right-clicking → "Vorschau erstellen" to publish.
    public ulong? AnnouncementsDraftChannelId { get; set; }
    public ulong? CommandBridgeChannelId { get; set; }
    public ulong? DiplomacyChannelId { get; set; }
    public ulong? RaidReportsChannelId { get; set; }
    public ulong? RemindersChannelId { get; set; }
    public ulong? RemindersAlliesChannelId { get; set; }
    public ulong? RemindersServicesChannelId { get; set; }
    public ulong? RulesDeChannelId { get; set; }
    public ulong? RulesEnChannelId { get; set; }
    public ulong? RoeViolationsChannelId { get; set; }
    public ulong? ShieldReminderChannelId { get; set; }
    public ulong? UserNotificationsChannelId { get; set; }
    public ulong? AnonymousMessagesChannelId { get; set; }
    // Parent channel every ticket thread gets created under. Staff visibility comes from
    // the guild's own Discord permission setup on this channel (Manage Threads), not from
    // anything our bot manages — see the Tickets plan section for why.
    public ulong? TicketsChannelId { get; set; }
    public ulong? BotSupportChannelId { get; set; }
    public ulong? CommandStaffJobsChannelId { get; set; }

    // The posted Command Bridge hub message, so /post-command-bridge can edit it in
    // place instead of re-posting a duplicate every time it's re-run.
    public ulong? CommandBridgeMessageId { get; set; }

    // Free-text "Anweisungen von {CommandStaff}" section shown on the Territory Capture
    // digest — per-guild wording, not hardcoded (e.g. attendance/armada expectations).
    public string? TerritoryCaptureInstructions { get; set; }

    // Roles
    public ulong? CommandStaffRoleId { get; set; }
    public ulong? DiplomatRoleId { get; set; }
    public ulong? MemberRoleId { get; set; }
    public ulong? BoardingRoleId { get; set; }
    public ulong? CrewsRoleId { get; set; }
    public ulong? BetaTesterRoleId { get; set; }
    public ulong? HoshiTesterRoleId { get; set; }
    public ulong? AlertsRoleId { get; set; }
    public ulong? WarningsRoleId { get; set; }

    // Territory Capture zone-slot roles — slot N's role goes to every member with an
    // owned zone in that slot this week (see TerritoryCaptureRoleSyncJob). Fixed 1-5, not
    // a child table: an alliance's "Zone Slots" cap in-game is always 5.
    public ulong? ZoneSlot1RoleId { get; set; }
    public ulong? ZoneSlot2RoleId { get; set; }
    public ulong? ZoneSlot3RoleId { get; set; }
    public ulong? ZoneSlot4RoleId { get; set; }
    public ulong? ZoneSlot5RoleId { get; set; }

    // STFC alliance rank roles — see assets/ranks/ranks.md for the fixed 5-rank list this
    // game defines. Not consumed by any bot logic yet (same "not built yet" status as
    // several channel/role fields above).
    public ulong? AdmiralRoleId { get; set; }
    public ulong? CommodoreRoleId { get; set; }
    public ulong? PremierRoleId { get; set; }
    public ulong? OperativeRoleId { get; set; }
    public ulong? AgentRoleId { get; set; }

    // Stamped when a guild admin completes (or explicitly finishes, even having skipped
    // steps) the Setup Wizard — drives the "needs setup" nudge on Guilds/Index.razor.
    public DateTimeOffset? SetupCompletedAt { get; set; }

    // The category the Setup Wizard's Core-settings step last used/created for
    // auto-created channels — remembered so it doesn't need re-selecting (or
    // re-creating!) every time that step runs. Null covers both "never set" and an
    // explicit "server root" choice — both default to the same behavior either way.
    public ulong? DefaultChannelCategoryId { get; set; }

    // Looks up a ZoneSlotNRoleId field by its slot number (1-5) — shared by
    // TerritoryCaptureRoleSyncJob and TerritoryCaptureDigestService so both index into the
    // same 5 fixed fields the same way.
    public ulong? GetZoneSlotRoleId(int slotIndex) => slotIndex switch
    {
        1 => ZoneSlot1RoleId,
        2 => ZoneSlot2RoleId,
        3 => ZoneSlot3RoleId,
        4 => ZoneSlot4RoleId,
        5 => ZoneSlot5RoleId,
        _ => null,
    };
}
