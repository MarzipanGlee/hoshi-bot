namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // The crowd-sourced STFC-news event-date confirmation flow: the shared post embed
    // (StfcNewsMessageBuilder) and the personal ephemeral replies/date modal
    // (StfcNewsButtonModule / StfcNewsModalModule).
    public static class News
    {
        // Doubles as the Enter-Date button label and the date modal's title.
        public static string EnterDateTitle(Language lang) =>
            MessageCatalog.Format(lang, "News.EnterDateTitle");

        public static string DateInputLabel(Language lang) =>
            MessageCatalog.Format(lang, "News.DateInputLabel");

        public static string DatePlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "News.DatePlaceholder");

        public static string DateParseError(Language lang) =>
            MessageCatalog.Format(lang, "News.DateParseError");

        public static string PostNotFound(Language lang) =>
            MessageCatalog.Format(lang, "News.PostNotFound");

        public static string AlreadyConfirmed(Language lang) =>
            MessageCatalog.Format(lang, "News.AlreadyConfirmed");

        public static string CannotConfirmOwn(Language lang) =>
            MessageCatalog.Format(lang, "News.CannotConfirmOwn");

        public static string FinalConfirmation(Language lang) =>
            MessageCatalog.Format(lang, "News.FinalConfirmation");

        public static string ConfirmationRecorded(Language lang, int count, int required) =>
            MessageCatalog.Format(lang, "News.ConfirmationRecorded", ("count", count), ("required", required));

        public static string DateSubmitted(Language lang, DateOnly date) =>
            MessageCatalog.Format(lang, "News.DateSubmitted", ("date", date));

        public static string ResolvedBody(Language lang, string title, string link, DateOnly date) =>
            MessageCatalog.Format(lang, "News.ResolvedBody", ("title", title), ("link", link), ("date", date));

        // user: the submitter's pre-built mention markup.
        public static string SuggestedBody(Language lang, string title, string link, string user, DateOnly date, int count, int required) =>
            MessageCatalog.Format(lang, "News.SuggestedBody",
                ("title", title), ("link", link), ("user", user), ("date", date), ("count", count), ("required", required));

        public static string NewPostBody(Language lang, string title, string link) =>
            MessageCatalog.Format(lang, "News.NewPostBody", ("title", title), ("link", link));

        public static string ConfirmButton(Language lang) =>
            MessageCatalog.Format(lang, "News.ConfirmButton");

        public static string EditButton(Language lang) =>
            MessageCatalog.Format(lang, "News.EditButton");
    }
}
