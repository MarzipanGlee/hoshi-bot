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
        // The German names are legacy's own — the staff bridge was already "führungsstab", which is
        // where the Senior Staff role's name came from.
        public static string CommandBridge(Language lang) =>
            MessageCatalog.Format(lang, "ChannelName.CommandBridge");

        public static string CommandBridgeStaff(Language lang) =>
            MessageCatalog.Format(lang, "ChannelName.CommandBridgeStaff");

        public static string CommandBridgeFriends(Language lang) =>
            MessageCatalog.Format(lang, "ChannelName.CommandBridgeFriends");

        // Guild-wide channels, so these resolve with the GUILD's language. Prefixed with the bot's
        // name because they sit at the server root among channels the guild made itself — a bare
        // "log" says nothing about who writes to it.
        public static string GuildLog(Language lang) =>
            MessageCatalog.Format(lang, "ChannelName.GuildLog");

        public static string GuildAdmin(Language lang) =>
            MessageCatalog.Format(lang, "ChannelName.GuildAdmin");

        public static string GuildUserLog(Language lang) =>
            MessageCatalog.Format(lang, "ChannelName.GuildUserLog");

        // Same word in both languages, but a catalog key all the same: the next locale will not be.
        public static string Boarding(Language lang) =>
            MessageCatalog.Format(lang, "ChannelName.Boarding");

        public static string AbsenceReport(Language lang) =>
            MessageCatalog.Format(lang, "ChannelName.AbsenceReport");

        public static string AbsenceReportStaff(Language lang) =>
            MessageCatalog.Format(lang, "ChannelName.AbsenceReportStaff");

        public static string RoeViolations(Language lang) =>
            MessageCatalog.Format(lang, "ChannelName.RoeViolations");
    }
}
