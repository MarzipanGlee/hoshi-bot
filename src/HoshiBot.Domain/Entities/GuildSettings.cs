namespace HoshiBot.Domain.Entities;

// One row per guild — global/wizard-level channel/role IDs not tied to one specific
// feature. Per-feature settings (Absences, Announcements, Diplomacy, Tickets, etc.) live in
// GuildFeatureSettingSnowflake/Text instead (see GuildFeatureSettingsService) — this entity
// only keeps: the audience selection itself, wizard/setup metadata, and channel/role IDs
// used across multiple features or not tied to any GuildFeature at all. Alert channel/role
// pairs (a genuinely multi-value setting) live in their own child table — see
// GuildAlertChannel.
public class GuildSettings
{
    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    // Which audience(s) this guild serves — set explicitly via the Setup Wizard's Audience
    // step or the Global Settings page, not derived from GuildAlliance/GuildServer/
    // GuildVeilGroup link existence (that was ambiguous: "hasn't set up Scope yet" vs.
    // "genuinely Community-only" looked identical). See GuildAudience for why this is UX
    // filtering only, never a runtime feature gate.
    public GuildAudience Audiences { get; set; }

    // Channels
    public ulong? LogChannelId { get; set; }
    public ulong? AdminChannelId { get; set; }
    public ulong? UserLogChannelId { get; set; }
    // The two persistent, edited-in-place "Abwesenheiten" report messages — tracked so
    // AbsenceService.RefreshReportsAsync can edit them in place instead of re-posting,
    // same role CommandBridgeMessageId plays for the hub message.
    public ulong? AbsencesReportMessageId { get; set; }
    public ulong? AbsencesReportStaffMessageId { get; set; }
    public ulong? AllianceBoardingChannelId { get; set; }
    public ulong? CommandBridgeChannelId { get; set; }
    public ulong? RemindersChannelId { get; set; }
    public ulong? RemindersAlliesChannelId { get; set; }
    public ulong? RemindersServicesChannelId { get; set; }
    public ulong? RulesDeChannelId { get; set; }
    public ulong? RulesEnChannelId { get; set; }
    public ulong? UserNotificationsChannelId { get; set; }
    public ulong? BotSupportChannelId { get; set; }
    public ulong? CommandStaffJobsChannelId { get; set; }

    // The posted Command Bridge hub message, so /post-command-bridge can edit it in
    // place instead of re-posting a duplicate every time it's re-run.
    public ulong? CommandBridgeMessageId { get; set; }

    // Roles
    public ulong? CommandStaffRoleId { get; set; }
    public ulong? MemberRoleId { get; set; }
    public ulong? BoardingRoleId { get; set; }
    public ulong? CrewsRoleId { get; set; }
    public ulong? BetaTesterRoleId { get; set; }
    public ulong? HoshiTesterRoleId { get; set; }
    public ulong? WarningsRoleId { get; set; }

    // Stamped when a guild admin completes (or explicitly finishes, even having skipped
    // steps) the Setup Wizard — drives the "needs setup" nudge on Guilds/Index.razor.
    public DateTimeOffset? SetupCompletedAt { get; set; }

    // The category the Setup Wizard's Core-settings step last used/created for
    // auto-created channels — remembered so it doesn't need re-selecting (or
    // re-creating!) every time that step runs. Null covers both "never set" and an
    // explicit "server root" choice — both default to the same behavior either way.
    public ulong? DefaultChannelCategoryId { get; set; }
}
