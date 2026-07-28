namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Member-onboarding player-link DMs (MemberOnboardingService): the outreach message,
    // its buttons and the confirmation/error replies.
    public static class Onboarding
    {
        public static string OutreachGuess(Language lang, string player) =>
            MessageCatalog.Format(lang, "Onboarding.OutreachGuess", ("player", player));

        public static string OutreachAsk(Language lang) =>
            MessageCatalog.Format(lang, "Onboarding.OutreachAsk");

        public static string ConfirmButton(Language lang) =>
            MessageCatalog.Format(lang, "Onboarding.ConfirmButton");

        public static string OtherPlayerButton(Language lang) =>
            MessageCatalog.Format(lang, "Onboarding.OtherPlayerButton");

        public static string EnterNameButton(Language lang) =>
            MessageCatalog.Format(lang, "Onboarding.EnterNameButton");

        public static string Linked(Language lang, string player) =>
            MessageCatalog.Format(lang, "Onboarding.Linked", ("player", player));

        public static string PlayerGone(Language lang) =>
            MessageCatalog.Format(lang, "Onboarding.PlayerGone");

        public static string RequestStale(Language lang) =>
            MessageCatalog.Format(lang, "Onboarding.RequestStale");

        public static string NameNotFound(Language lang, string name) =>
            MessageCatalog.Format(lang, "Onboarding.NameNotFound", ("name", name));

        public static string NameAmbiguous(Language lang, string name) =>
            MessageCatalog.Format(lang, "Onboarding.NameAmbiguous", ("name", name));
    }
}
