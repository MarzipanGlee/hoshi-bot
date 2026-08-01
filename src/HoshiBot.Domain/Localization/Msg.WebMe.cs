namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // The /me area's own pages ("Web.Me.*") — Pages/Me/Index.razor and MemberLoreSelf.razor.
    // Components consume the layout-cascaded Language ([CascadingParameter] Language Lang),
    // even though MeLayout has no BootstrapBlazorRoot — the cascade still works. The "My
    // Language" select's own option labels (native language names) stay dynamic via
    // Languages.NativeName; only the wrapper prose around it is catalog-driven.
    public static class WebMe
    {
        public static string Lead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.Lead");

        // The banner shown after an OAuth account-link attempt redirects back here
        // (?link=ok|already|self|noplayers|failed).
        public static string LinkOk(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.LinkOk");

        public static string LinkAlready(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.LinkAlready");

        public static string LinkSelf(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.LinkSelf");

        public static string LinkNoPlayers(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.LinkNoPlayers");

        public static string LinkFailed(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.LinkFailed");

        public static string PlayerAccountsTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.PlayerAccountsTitle");

        public static string PlayerAccountsLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.PlayerAccountsLead");

        public static string NoPlayersYet(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.NoPlayersYet");

        // "via {name}" next to a player another linked account owns.
        public static string Via(Language lang, string name) =>
            MessageCatalog.Format(lang, "Web.Me.Via", ("name", name));

        public static string ConnectPlayerLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.ConnectPlayerLabel");

        public static string SearchPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.SearchPlaceholder");

        public static string PlayerAlreadyLinked(Language lang, string name) =>
            MessageCatalog.Format(lang, "Web.Me.PlayerAlreadyLinked", ("name", name));

        public static string CommunitiesTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.CommunitiesTitle");

        public static string NoCommunities(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.NoCommunities");

        public static string ConnectPlayerToGetRoles(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.ConnectPlayerToGetRoles");

        public static string PlayingHereAs(Language lang, string name) =>
            MessageCatalog.Format(lang, "Web.Me.PlayingHereAs", ("name", name));

        public static string PlayingHereAsLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.PlayingHereAsLabel");

        public static string LanguageTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.LanguageTitle");

        public static string LanguageLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.LanguageLead");

        // The default "inherit" option's wrapper text; the parenthesized part is one of
        // YourDiscordLanguage/DiscordLanguageLabel below.
        public static string Automatic(Language lang, string label) =>
            MessageCatalog.Format(lang, "Web.Me.Automatic", ("label", label));

        public static string YourDiscordLanguage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.YourDiscordLanguage");

        public static string DiscordLanguageLabel(Language lang, string name) =>
            MessageCatalog.Format(lang, "Web.Me.DiscordLanguageLabel", ("name", name));

        public static string DiscordAccountsTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.DiscordAccountsTitle");

        public static string DiscordAccountsLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.DiscordAccountsLead");

        public static string SignedInBadge(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.SignedInBadge");

        public static string LinkAnotherAccount(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.LinkAnotherAccount");

        // Contains an <em> tag — render with @((MarkupString)...).
        public static string LinkAnotherAccountHint(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.LinkAnotherAccountHint");

        // MemberLoreSelf.razor ("My Profile").
        public static string ProfileTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.ProfileTitle");

        public static string ProfileHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.ProfileHeading");

        public static string ProfileLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.ProfileLead");

        public static string NoCards(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.NoCards");

        public static string PreferredNameLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.PreferredNameLabel");

        public static string NicknamesLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.NicknamesLabel");

        public static string LanguagesLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.LanguagesLabel");

        public static string InterestsLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.InterestsLabel");

        public static string BackgroundLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.BackgroundLabel");

        public static string SavedConfirmation(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.SavedConfirmation");

        public static string NicknameSuffixTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.NicknameSuffixTitle");

        // Contains inline <code> markup (the rendered example) — render via MarkupString.
        public static string NicknameSuffixLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.NicknameSuffixLead");

        public static string NicknameSuffixPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.NicknameSuffixPlaceholder");

        public static string PeerLoreHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.PeerLoreHeading");

        public static string PeerLoreLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.PeerLoreLead");

        public static string RunningJokesBadge(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.RunningJokesBadge");

        public static string OkToTeaseBadge(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.OkToTeaseBadge");

        public static string HideNotesLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.HideNotesLabel");

        public static string CommunityFallback(Language lang) =>
            MessageCatalog.Format(lang, "Web.Me.CommunityFallback");
    }
}
