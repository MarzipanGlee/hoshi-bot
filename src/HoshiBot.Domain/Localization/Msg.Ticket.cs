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

        // Admin permission-issue notifications (NotificationDispatcher action/hint pairs).
        public static string ActionCreate(Language lang) =>
            MessageCatalog.Format(lang, "Ticket.ActionCreate");

        public static string ActionSendWelcome(Language lang) =>
            MessageCatalog.Format(lang, "Ticket.ActionSendWelcome");

        public static string ActionAddCommander(Language lang) =>
            MessageCatalog.Format(lang, "Ticket.ActionAddCommander");

        public static string ActionClose(Language lang) =>
            MessageCatalog.Format(lang, "Ticket.ActionClose");

        public static string HintCreateThreads(Language lang, string channel) =>
            MessageCatalog.Format(lang, "Ticket.HintCreateThreads", ("channel", channel));

        public static string HintThreadPermission(Language lang, string thread) =>
            MessageCatalog.Format(lang, "Ticket.HintThreadPermission", ("thread", thread));

        public static string HintManageThreads(Language lang, string thread) =>
            MessageCatalog.Format(lang, "Ticket.HintManageThreads", ("thread", thread));
    }
}
