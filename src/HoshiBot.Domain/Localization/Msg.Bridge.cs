namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Command Bridge hub flows (CommandBridgeButtonModule): raid/shield prompts, the
    // contact-command-staff step, unread announcements, RoE entry buttons, alert opt-ins.
    public static class Bridge
    {
        public static string RaidTargetPrompt(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.RaidTargetPrompt");

        public static string RaidModalTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.RaidModalTitle");

        public static string LocationLabel(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.LocationLabel");

        public static string SystemPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.SystemPlaceholder");

        public static string AttackerLabel(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AttackerLabel");

        public static string AttackerPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AttackerPlaceholder");

        public static string ShieldModalTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ShieldModalTitle");

        public static string ShieldDurationLabel(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ShieldDurationLabel");

        public static string ShieldDurationPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ShieldDurationPlaceholder");

        public static string DraftNotFound(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.DraftNotFound");

        public static string DraftUnknownKind(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.DraftUnknownKind");

        public static string Cancelled(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.Cancelled");

        public static string ContactTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ContactTitle");

        // options: the pre-joined ContactTicketOption/ContactAnonymousOption lines.
        public static string ContactIntro(Language lang, string name, string options) =>
            MessageCatalog.Format(lang, "Bridge.ContactIntro", ("name", name), ("options", options));

        public static string ContactTicketOption(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ContactTicketOption");

        public static string ContactAnonymousOption(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ContactAnonymousOption");

        // Doubles as the contact-step button label and the modal title.
        public static string TicketOpen(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.TicketOpen");

        public static string AnonymousMessage(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AnonymousMessage");

        public static string FeatureDisabledHere(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.FeatureDisabledHere");

        public static string SubjectLabel(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.SubjectLabel");

        public static string SubjectPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.SubjectPlaceholder");

        public static string MessageLabel(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.MessageLabel");

        public static string MessagePlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.MessagePlaceholder");

        public static string AnnouncementsUnreadTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AnnouncementsUnreadTitle");

        public static string AnnouncementsAllRead(Language lang, string name) =>
            MessageCatalog.Format(lang, "Bridge.AnnouncementsAllRead", ("name", name));

        public static string AnnouncementsUnreadIntro(Language lang, string name, string list) =>
            MessageCatalog.Format(lang, "Bridge.AnnouncementsUnreadIntro", ("name", name), ("list", list));

        public static string RoeToMe(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.RoeToMe");

        public static string RoeFromMe(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.RoeFromMe");

        public static string RoeByOwnPlayer(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.RoeByOwnPlayer");

        public static string RoePromptBody(Language lang, string name) =>
            MessageCatalog.Format(lang, "Bridge.RoePromptBody", ("name", name));

        public static string RoeOtherPrompt(Language lang, string name) =>
            MessageCatalog.Format(lang, "Bridge.RoeOtherPrompt", ("name", name));

        public static string AlertsTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AlertsTitle");

        public static string AlertsNoRoles(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AlertsNoRoles");

        public static string AlertsOn(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AlertsOn");

        public static string AlertsOff(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AlertsOff");

        public static string AlertsIntro(Language lang, string list) =>
            MessageCatalog.Format(lang, "Bridge.AlertsIntro", ("list", list));
    }
}
