using HoshiBot.Domain.Entities;

namespace HoshiBot.Data.Seeding;

// Real settings for the user's own guild, ported from hoshi-bot-yagpdb's
// Commands/static_data/definitions-snowflakes.yag (the legacy bot's single hardcoded
// guild). LogChannelId is the exception — it's not in that file (only referenced there
// dynamically via $defs.LogChannel); its literal value comes from
// Commands/static_data/definitions-common.yag instead.
public static class GuildSettingsSeedData
{
    public const ulong GuildId = 793375182596866079;
    public const string GuildName = "Lost Falcons";

    public static GuildSettings CreateSettings() => new()
    {
        GuildId = GuildId,

        LogChannelId = 1251856032339066942,
        AdminChannelId = 1251527491399454791,
        UserLogChannelId = 1251050019235299448,
        AbsencesReportChannelId = 793380668682928129,
        AbsencesReportStaffChannelId = 1269303372423368826,
        AllianceBoardingChannelId = 955406358356852746,
        AnnouncementsChannelId = 793377058809577492,
        AnnouncementsRemindersChannelId = 942406102325264474,
        CommandBridgeChannelId = 1251810911451349095,
        DiplomacyChannelId = 1180100541670498365,
        RaidReportsChannelId = 1268360555564105921,
        RemindersChannelId = 832991289423167579,
        RemindersAlliesChannelId = 1273592443954003968,
        RemindersServicesChannelId = 810175634096783411,
        RulesDeChannelId = 803965908309245962,
        RulesEnChannelId = 957288857915752468,
        RoeViolationsChannelId = 1022092671378001920,
        ShieldReminderChannelId = 1252972665044603083,
        UserNotificationsChannelId = 942406102325264474,
        AnonymousMessagesChannelId = 1254418233788858480,
        BotSupportChannelId = 1255819027570495488,
        CommandStaffJobsChannelId = 1267856202046636165,

        CommandStaffRoleId = 813727551678840884,
        DiplomatRoleId = 829693359874375710,
        MemberRoleId = 793383681233518633,
        BoardingRoleId = 1269760517807800320,
        CrewsRoleId = 1044929004035113070,
        BetaTesterRoleId = 1253341776970776637,
        HoshiTesterRoleId = 1268128662457286687,
        AlertsRoleId = 1253175695354364066,
        WarningsRoleId = 793383681233518633,

        ZoneSlot1RoleId = 1275018847417536554,
        ZoneSlot2RoleId = 1275019040108318791,
        ZoneSlot3RoleId = 1275019101265465427,
        ZoneSlot4RoleId = 1275019200196509706,
        ZoneSlot5RoleId = 1275019288314380413,

        CommodoreRoleId = 1255229709546033294,
    };

    public static readonly (GuildAlertChannelKind Kind, ulong ChannelId, ulong RoleId)[] AlertChannels =
    [
        (GuildAlertChannelKind.Raid, 1252972665044603083, 793383681233518633),
        (GuildAlertChannelKind.Raid, 1253299015181795430, 936759742691434526),
        (GuildAlertChannelKind.Shield, 1252972665044603083, 793383681233518633),
        (GuildAlertChannelKind.Shield, 793376920343019530, 936759742691434526),
    ];
}
