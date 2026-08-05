namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Tickets (TicketService).
    public static class Ticket
    {
        public static string CloseButton(Language lang) =>
            MessageCatalog.Format(lang, "Ticket.CloseButton");

        public static string AddCommanderMenu(Language lang) =>
            MessageCatalog.Format(lang, "Ticket.AddCommanderMenu");

        public static string Welcome(Language lang, string name) =>
            MessageCatalog.Format(lang, "Ticket.Welcome", ("name", name));

        public static string ChannelNotConfigured(Language lang) =>
            MessageCatalog.Format(lang, "Ticket.ChannelNotConfigured");

        public static string Misconfigured(Language lang) =>
            MessageCatalog.Format(lang, "Ticket.Misconfigured");

        public static string Created(Language lang, string thread) =>
            MessageCatalog.Format(lang, "Ticket.Created", ("thread", thread));

        public static string NotFound(Language lang) =>
            MessageCatalog.Format(lang, "Ticket.NotFound");

        public static string AddFailed(Language lang) =>
            MessageCatalog.Format(lang, "Ticket.AddFailed");

        public static string CommanderAdded(Language lang, string member) =>
            MessageCatalog.Format(lang, "Ticket.CommanderAdded", ("member", member));

        public static string AlreadyClosed(Language lang) =>
            MessageCatalog.Format(lang, "Ticket.AlreadyClosed");

        public static string CloseFailed(Language lang) =>
            MessageCatalog.Format(lang, "Ticket.CloseFailed");

        public static string Closed(Language lang) =>
            MessageCatalog.Format(lang, "Ticket.Closed");







    }
}
