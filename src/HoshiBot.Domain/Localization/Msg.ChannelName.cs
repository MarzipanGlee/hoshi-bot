namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Default names for channels the admin UI offers to CREATE.
    //
    // Localized, and resolved with the SCOPE's language rather than the admin's: the channel is a
    // permanent artifact everyone in the guild reads, so it follows the same rule as any public post
    // the bot makes. An English-speaking alliance was being offered a channel called
    // "abwesenheiten" because the default was a German string literal in the editor.
    //
    // Discord lowercases and slugifies these on creation, so they are written the way they will end
    // up. Umlauts survive — legacy's own channels use them.
    public static class ChannelName
    {
        public static string AbsenceReport(Language lang) =>
            MessageCatalog.Format(lang, "ChannelName.AbsenceReport");

        public static string AbsenceReportStaff(Language lang) =>
            MessageCatalog.Format(lang, "ChannelName.AbsenceReportStaff");

        public static string RoeViolations(Language lang) =>
            MessageCatalog.Format(lang, "ChannelName.RoeViolations");
    }
}
