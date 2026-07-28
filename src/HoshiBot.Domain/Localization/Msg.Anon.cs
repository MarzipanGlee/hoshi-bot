namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Anonymous member-to-staff messages (AnonymousMessageService).
    public static class Anon
    {
        public static string ChannelNotConfigured(Language lang) =>
            MessageCatalog.Format(lang, "Anon.ChannelNotConfigured");

        // message: the member's own text, appended below the attribution phrase.
        public static string Body(Language lang, string message) =>
            MessageCatalog.Format(lang, "Anon.Body", ("message", message));

        public static string ActionSend(Language lang) =>
            MessageCatalog.Format(lang, "Anon.ActionSend");

        public static string HintChannelPermission(Language lang, string channel) =>
            MessageCatalog.Format(lang, "Anon.HintChannelPermission", ("channel", channel));

        public static string SendFailed(Language lang) =>
            MessageCatalog.Format(lang, "Anon.SendFailed");

        public static string Sent(Language lang) =>
            MessageCatalog.Format(lang, "Anon.Sent");
    }
}
