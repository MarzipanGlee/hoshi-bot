namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Player linking slash commands (PlayerModule): /link-player and /set-my-alliance.
    public static class Player
    {
        public static string ServerNotFound(Language lang, string server) =>
            MessageCatalog.Format(lang, "Player.ServerNotFound", ("server", server));

        public static string Linked(Language lang, string player, string server) =>
            MessageCatalog.Format(lang, "Player.Linked", ("player", player), ("server", server));

        public static string NoLinkedPlayer(Language lang) =>
            MessageCatalog.Format(lang, "Player.NoLinkedPlayer");

        public static string AllianceNotFound(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Player.AllianceNotFound", ("tag", tag));

        public static string AllianceSet(Language lang, string name, string tag) =>
            MessageCatalog.Format(lang, "Player.AllianceSet", ("name", name), ("tag", tag));
    }
}
