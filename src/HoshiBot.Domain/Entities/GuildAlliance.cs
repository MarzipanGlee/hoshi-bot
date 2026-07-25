namespace HoshiBot.Domain.Entities;

// The guild-specific Discord config for one shared alliance — the role-ID mappings (e.g.
// "which role marks our officers") plus the alliance-scoped channels and Command Bridge hub
// state. No diplomacy status here, that's StfcAllianceDiplomacy, an in-game fact independent
// of any guild. These are feature-agnostic per-alliance attributes; per-feature settings live
// in GuildFeatureSettingSnowflake/Text instead (see GuildFeatureSettingsService).
public class GuildAlliance
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public int StfcAllianceId { get; set; }

    public StfcAlliance StfcAlliance { get; set; } = null!;

    // Roles
    public ulong? MemberRoleId { get; set; }
    public ulong? OfficerRoleId { get; set; }
    public ulong? DiplomatRoleId { get; set; }
    public ulong? BoardingRoleId { get; set; }

    // Channels — alliance-scoped config formerly on GuildSettings (moved so a coalition guild
    // can configure each linked alliance independently).
    public ulong? AllianceBoardingChannelId { get; set; }
    public ulong? RemindersAlliesChannelId { get; set; }
    public ulong? RulesDeChannelId { get; set; }
    public ulong? RulesEnChannelId { get; set; }
    public ulong? UserNotificationsChannelId { get; set; }
    public ulong? BotSupportChannelId { get; set; }
    public ulong? CommandStaffJobsChannelId { get; set; }

    // The category new channels get auto-created under (e.g. by the Setup Wizard) when none is
    // picked explicitly. Null covers both "never set" and an explicit "server root" choice.
    public ulong? DefaultChannelCategoryId { get; set; }

    // Command Bridge — one channel + one posted hub message id per bridge, so a (re)publish can
    // edit in place instead of re-posting a duplicate. Each linked alliance has its own bridges.
    public ulong? CommandBridgeChannelId { get; set; }
    public ulong? StaffCommandBridgeChannelId { get; set; }
    public ulong? FriendsCommandBridgeChannelId { get; set; }
    public ulong? CommandBridgeMessageId { get; set; }
    public ulong? StaffCommandBridgeMessageId { get; set; }
    public ulong? FriendsCommandBridgeMessageId { get; set; }
}
