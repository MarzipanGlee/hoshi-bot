namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Member-lore DM interviews (MemberInterviewService). Only the fixed DM messages live
    // here — the interview itself is LLM chat that mirrors the member's language, so its
    // system prompt stays in code (docs/localization-plan.md).
    public static class Interview
    {
        public static string DeclineButton(Language lang) =>
            MessageCatalog.Format(lang, "Interview.DeclineButton");

        public static string Opener(Language lang, string botName) =>
            MessageCatalog.Format(lang, "Interview.Opener", ("botName", botName));

        public static string OptOutClose(Language lang) =>
            MessageCatalog.Format(lang, "Interview.OptOutClose");

        public static string DeclineClose(Language lang) =>
            MessageCatalog.Format(lang, "Interview.DeclineClose");
    }
}
