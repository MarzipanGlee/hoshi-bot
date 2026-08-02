namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Member-lore DM interviews (MemberInterviewService). Only the button label and the two
    // closers live here — the interview itself is LLM chat that mirrors the member's language,
    // so its system prompt stays in code (docs/localization-plan.md), and so does the opener:
    // it names the alliance and gets tuned often, so InterviewOpener keeps one English constant
    // and has the model translate it per member instead.
    public static class Interview
    {
        public static string DeclineButton(Language lang) =>
            MessageCatalog.Format(lang, "Interview.DeclineButton");

        public static string OptOutClose(Language lang) =>
            MessageCatalog.Format(lang, "Interview.OptOutClose");

        public static string DeclineClose(Language lang) =>
            MessageCatalog.Format(lang, "Interview.DeclineClose");
    }
}
