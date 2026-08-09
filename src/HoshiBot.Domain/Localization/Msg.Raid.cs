namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // The weekly raid report ("Raid.*") — the Monday post summarising the week that just ended.
    // Separate from Msg.Alert, which is the live raid/shield alert traffic: this is a scheduled
    // public post in the alliance's language, not a message to one member.
    public static class Raid
    {
        public static string ReportTitle(Language lang, int week) =>
            MessageCatalog.Format(lang, "Raid.ReportTitle", ("week", week));

        public static string ReportIntro(Language lang) =>
            MessageCatalog.Format(lang, "Raid.ReportIntro");

        public static string ReportNone(Language lang) =>
            MessageCatalog.Format(lang, "Raid.ReportNone");

        public static string ReportPeriod(Language lang) =>
            MessageCatalog.Format(lang, "Raid.ReportPeriod");

        public static string ReportPeriodValue(Language lang, string start, string end) =>
            MessageCatalog.Format(lang, "Raid.ReportPeriodValue", ("start", start), ("end", end));

        public static string ReportCountRaids(Language lang) =>
            MessageCatalog.Format(lang, "Raid.ReportCountRaids");

        public static string ReportCountCommanders(Language lang) =>
            MessageCatalog.Format(lang, "Raid.ReportCountCommanders");

        public static string ReportShieldHint(Language lang) =>
            MessageCatalog.Format(lang, "Raid.ReportShieldHint");

        public static string ReportShieldHintRaids(Language lang, string channel) =>
            MessageCatalog.Format(lang, "Raid.ReportShieldHintRaids", ("channel", channel));

        public static string ReportShieldHintNone(Language lang, string channel) =>
            MessageCatalog.Format(lang, "Raid.ReportShieldHintNone", ("channel", channel));

        // One bullet per raid. The attacker is optional on a report, so it gets its own string
        // rather than an empty " von **" tail.
        public static string ReportEntry(Language lang, string when, string duration, string system) =>
            MessageCatalog.Format(lang, "Raid.ReportEntry", ("when", when), ("duration", duration), ("system", system));

        public static string ReportEntryAttacker(Language lang, string when, string duration, string system, string attacker) =>
            MessageCatalog.Format(lang, "Raid.ReportEntryAttacker",
                ("when", when), ("duration", duration), ("system", system), ("attacker", attacker));
    }
}
