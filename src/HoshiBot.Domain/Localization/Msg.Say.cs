namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Admin "/hoshi-say" ephemeral replies (HoshiSayModule).
    public static class Say
    {
        public static string NoRoleConfigured(Language lang) =>
            MessageCatalog.Format(lang, "Say.NoRoleConfigured");

        public static string RoleRequired(Language lang, string role) =>
            MessageCatalog.Format(lang, "Say.RoleRequired", ("role", role));

        public static string ComposeFailed(Language lang) =>
            MessageCatalog.Format(lang, "Say.ComposeFailed");

        public static string Posted(Language lang, string text) =>
            MessageCatalog.Format(lang, "Say.Posted", ("text", text));
    }
}
