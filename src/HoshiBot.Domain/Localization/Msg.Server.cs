namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Game-server status change announcements (ServerStatusNotifyJob).
    public static class Server
    {
        public static string StatusChangeTitle(Language lang) =>
            MessageCatalog.Format(lang, "Server.StatusChangeTitle");

        public static string Maintenance(Language lang, string server) =>
            MessageCatalog.Format(lang, "Server.Maintenance", ("server", server));

        public static string Down(Language lang, string server) =>
            MessageCatalog.Format(lang, "Server.Down", ("server", server));

        public static string BackOnline(Language lang, string server) =>
            MessageCatalog.Format(lang, "Server.BackOnline", ("server", server));
    }
}
