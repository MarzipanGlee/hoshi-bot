namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // The member join/leave entries written to the guild's log channel (MemberLogService). Guild
    // language: the log channel is staff-facing and guild-wide.
    public static class MemberLog
    {
        public static string Joined(Language lang, string user) =>
            MessageCatalog.Format(lang, "MemberLog.Joined", ("user", user));

        public static string Left(Language lang, string user) =>
            MessageCatalog.Format(lang, "MemberLog.Left", ("user", user));

        public static string FieldUserId(Language lang) =>
            MessageCatalog.Format(lang, "MemberLog.FieldUserId");

        public static string FieldGlobalName(Language lang) =>
            MessageCatalog.Format(lang, "MemberLog.FieldGlobalName");

        public static string FieldUsername(Language lang) =>
            MessageCatalog.Format(lang, "MemberLog.FieldUsername");
    }
}
