namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Hoshi's in-character canned replies (HoshiPersona / AiChat): the four "temporarily
    // unavailable" busy variants and the polite can't-answer fallback for a directly-addressed
    // question. Rendered in the same channel-scope language as the AI reply they substitute for.
    public static class Persona
    {
        public static string Busy1(Language lang) =>
            MessageCatalog.Format(lang, "Persona.Busy1");

        public static string Busy2(Language lang) =>
            MessageCatalog.Format(lang, "Persona.Busy2");

        public static string Busy3(Language lang) =>
            MessageCatalog.Format(lang, "Persona.Busy3");

        public static string Busy4(Language lang) =>
            MessageCatalog.Format(lang, "Persona.Busy4");

        public static string CannotAnswer(Language lang) =>
            MessageCatalog.Format(lang, "Persona.CannotAnswer");

        public static string CannotAnswerGreeting(Language lang, string name) =>
            MessageCatalog.Format(lang, "Persona.CannotAnswerGreeting", ("name", name));
    }
}
