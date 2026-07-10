using HoshiBot.Domain.Entities;

namespace HoshiBot.Data.Seeding;

// Real settings for the user's own guild, ported from hoshi-bot-yagpdb's
// Commands/static_data/definitions-snowflakes.yag (the legacy bot's single hardcoded
// guild). LogChannelId is the exception — it's not in that file (only referenced there
// dynamically via $defs.LogChannel); its literal value comes from
// Commands/static_data/definitions-common.yag instead.
//
// This is a single-alliance Discord, so every per-feature setting below is seeded under
// GuildAudience.Alliance — the only audience this real guild actually uses.
public static class GuildSettingsSeedData
{
    public const ulong GuildId = 793375182596866079;
    public const string GuildName = "Lost Falcons";

    public static GuildSettings CreateSettings() => new()
    {
        GuildId = GuildId,
        Audiences = GuildAudience.Alliance,

        LogChannelId = 1251856032339066942,
        AdminChannelId = 1251527491399454791,
        UserLogChannelId = 1251050019235299448,
        AllianceBoardingChannelId = 955406358356852746,
        CommandBridgeChannelId = 1251810911451349095,
        RemindersChannelId = 832991289423167579,
        RemindersAlliesChannelId = 1273592443954003968,
        RemindersServicesChannelId = 810175634096783411,
        RulesDeChannelId = 803965908309245962,
        RulesEnChannelId = 957288857915752468,
        UserNotificationsChannelId = 942406102325264474,
        BotSupportChannelId = 1255819027570495488,
        CommandStaffJobsChannelId = 1267856202046636165,

        CommandStaffRoleId = 813727551678840884,
        MemberRoleId = 793383681233518633,
        BoardingRoleId = 1269760517807800320,
        CrewsRoleId = 1044929004035113070,
        BetaTesterRoleId = 1253341776970776637,
        HoshiTesterRoleId = 1268128662457286687,
        WarningsRoleId = 793383681233518633,
    };

    // Per-feature settings that now live in GuildFeatureSettingSnowflakes/Texts instead of
    // flat GuildSettings columns — see GuildFeatureSettingsService.
    public static readonly (GuildFeature Feature, string Key, ulong Value)[] SnowflakeSettings =
    [
        (GuildFeature.Absences, AbsencesSettingKeys.ReportChannel, 793380668682928129),
        (GuildFeature.Absences, AbsencesSettingKeys.ReportStaffChannel, 1269303372423368826),
        (GuildFeature.Announcements, AnnouncementsSettingKeys.Channel, 793377058809577492),
        (GuildFeature.Announcements, AnnouncementsSettingKeys.RemindersChannel, 942406102325264474),
        (GuildFeature.Diplomacy, DiplomacySettingKeys.Channel, 1180100541670498365),
        (GuildFeature.Diplomacy, DiplomacySettingKeys.DiplomatRole, 829693359874375710),
        (GuildFeature.RaidAlerts, "Channel", 1268360555564105921),
        (GuildFeature.RoeViolationReports, RoeViolationReportsSettingKeys.Channel, 1022092671378001920),
        (GuildFeature.ShieldReminders, "Channel", 1252972665044603083),
        (GuildFeature.AnonymousMessaging, AnonymousMessagingSettingKeys.Channel, 1254418233788858480),
        (GuildFeature.AlertsOptIn, AlertsOptInSettingKeys.Role, 1253175695354364066),
        (GuildFeature.TerritoryCapture, TerritoryCaptureSettingKeys.ZoneSlot1Role, 1275018847417536554),
        (GuildFeature.TerritoryCapture, TerritoryCaptureSettingKeys.ZoneSlot2Role, 1275019040108318791),
        (GuildFeature.TerritoryCapture, TerritoryCaptureSettingKeys.ZoneSlot3Role, 1275019101265465427),
        (GuildFeature.TerritoryCapture, TerritoryCaptureSettingKeys.ZoneSlot4Role, 1275019200196509706),
        (GuildFeature.TerritoryCapture, TerritoryCaptureSettingKeys.ZoneSlot5Role, 1275019288314380413),
        (GuildFeature.TerritoryCapture, TerritoryCaptureSettingKeys.CommodoreRole, 1255229709546033294),
    ];

    public static readonly (GuildAlertChannelKind Kind, ulong ChannelId, ulong RoleId)[] AlertChannels =
    [
        (GuildAlertChannelKind.Raid, 1252972665044603083, 793383681233518633),
        (GuildAlertChannelKind.Raid, 1253299015181795430, 936759742691434526),
        (GuildAlertChannelKind.Shield, 1252972665044603083, 793383681233518633),
        (GuildAlertChannelKind.Shield, 793376920343019530, 936759742691434526),
    ];

    // This guild actively uses all 12 features today — seeded as enabled so the dev DB
    // mirrors real production state (features are off by default otherwise).
    public static readonly GuildFeature[] EnabledFeatures = Enum.GetValues<GuildFeature>();
}
