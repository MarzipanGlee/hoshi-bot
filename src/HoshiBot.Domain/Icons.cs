namespace HoshiBot.Domain;

// The bot's shared symbol vocabulary — the same four the legacy bot used, so a member who read the
// old absence report recognises the new one. They live here rather than beside any one feature
// because Data (the Command Bridge catalog), Discord (buttons, report lines) and the message
// catalog all render them, and duplicated literals drift: the absence "notifications off" label
// carried a 🔔 for months because it was copy-pasted from the "on" one.
//
// Same reasoning as AnnouncementSeverities' 🟩🟨🟥🟦 palette, one layer down: symbols carry no
// language, so they belong in code and not in the per-locale JSON. Localized labels compose them
// (see Msg.Absence.NotificationsOn) instead of embedding a copy.
public static class Icons
{
    // Whether reminders/alerts are delivered — used for absence reminders, shield reminders and
    // the alert opt-in alike.
    public const string RemindersOn = "🔔";
    public const string RemindersOff = "🔕";

    // Who can see something: everyone, or the command staff only.
    public const string Public = "📢";
    public const string StaffOnly = "🤐";
}
