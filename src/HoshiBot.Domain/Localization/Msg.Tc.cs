namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Territory Capture digests and reminders (TerritoryCaptureDigestService) plus the
    // unsubscribe button's replies and absence reasons (TerritoryCaptureButtonModule).
    public static class Tc
    {
        public static string WeeklyTitle(Language lang, string from, string to) =>
            MessageCatalog.Format(lang, "Tc.WeeklyTitle", ("from", from), ("to", to));

        public static string DailyTitle(Language lang) =>
            MessageCatalog.Format(lang, "Tc.DailyTitle");

        public static string TitleWithTag(Language lang, string title, string tag) =>
            MessageCatalog.Format(lang, "Tc.TitleWithTag", ("title", title), ("tag", tag));

        public static string ScheduleHeader(Language lang) =>
            MessageCatalog.Format(lang, "Tc.ScheduleHeader");

        public static string ScheduleRow(Language lang, int slot, string name, int tier, string neighbours, string day, long start, long end) =>
            MessageCatalog.Format(lang, "Tc.ScheduleRow",
                ("slot", slot), ("name", name), ("tier", tier), ("neighbours", neighbours), ("day", day), ("start", start), ("end", end));

        public static string TimesUnknown(Language lang, string zones) =>
            MessageCatalog.Format(lang, "Tc.TimesUnknown", ("zones", zones));

        public static string BridgeFallback(Language lang) =>
            MessageCatalog.Format(lang, "Tc.BridgeFallback");

        public static string DigestIntro(Language lang, string bridge) =>
            MessageCatalog.Format(lang, "Tc.DigestIntro", ("bridge", bridge));

        // Used when absence sign-off is off for this alliance: the intro stops before the "…or sign
        // off …" clause, since neither the buttons nor the Command Bridge's absence entry point
        // exist then. Deliberately a separate key rather than a conditional suffix — the two
        // sentences read differently in German, and the catalog is the place that decision lives.
        public static string DigestIntroNoSignOff(Language lang) =>
            MessageCatalog.Format(lang, "Tc.DigestIntroNoSignOff");

        public static string FieldSchedule(Language lang) =>
            MessageCatalog.Format(lang, "Tc.FieldSchedule");

        public static string FieldInstructions(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Tc.FieldInstructions", ("tag", tag));

        public static string UnsubscribeButton(Language lang, string zone) =>
            MessageCatalog.Format(lang, "Tc.UnsubscribeButton", ("zone", zone));

        public static string ReminderTitle(Language lang, string zone, long start) =>
            MessageCatalog.Format(lang, "Tc.ReminderTitle", ("zone", zone), ("start", start));

        // Who may contest a zone, by tier. Tiers 1-4 are covered, which is every tier the territory
        // catalog actually contains — legacy's own table stopped at 3 and simply said nothing for a
        // 4* zone. An unknown tier still returns "" so the reminder omits the line rather than
        // inventing a rule for a tier the game hasn't shipped.
        public static string TierDescription(Language lang, int tier)
        {
            var key = $"Tc.TierDescription{tier}";
            var text = MessageCatalog.Format(lang, key);
            return text == key ? "" : text;
        }


        // Sign-off-free counterpart of ReminderBody — just the window, no "please sign off" ask.

        public static string ServicesTitle(Language lang, string zone) =>
            MessageCatalog.Format(lang, "Tc.ServicesTitle", ("zone", zone));

        public static string ServicesGenericNudge(Language lang, string zone) =>
            MessageCatalog.Format(lang, "Tc.ServicesGenericNudge", ("zone", zone));

        public static string ServicesAllInOrder(Language lang, string zone, string list) =>
            MessageCatalog.Format(lang, "Tc.ServicesAllInOrder", ("zone", zone), ("list", list));

        public static string ServicesEnded(Language lang, string zone) =>
            MessageCatalog.Format(lang, "Tc.ServicesEnded", ("zone", zone));

        public static string ServicesMustHave(Language lang, string list) =>
            MessageCatalog.Format(lang, "Tc.ServicesMustHave", ("list", list));

        public static string ServicesNiceToHave(Language lang, string list) =>
            MessageCatalog.Format(lang, "Tc.ServicesNiceToHave", ("list", list));

        public static string AbsenceReasonGeneric(Language lang) =>
            MessageCatalog.Format(lang, "Tc.AbsenceReasonGeneric");

        public static string AbsenceReason(Language lang, string zone) =>
            MessageCatalog.Format(lang, "Tc.AbsenceReason", ("zone", zone));

        public static string AlreadyAbsent(Language lang, string name) =>
            MessageCatalog.Format(lang, "Tc.AlreadyAbsent", ("name", name));

        public static string AbsenceRecorded(Language lang, string name) =>
            MessageCatalog.Format(lang, "Tc.AbsenceRecorded", ("name", name));
    }
}
