namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // RoE violation reports (RoeViolationService and its modules).
    public static class Roe
    {
        public static string Title(Language lang) =>
            MessageCatalog.Format(lang, "Roe.Title");

        public static string VictimSteps(Language lang) =>
            MessageCatalog.Format(lang, "Roe.VictimSteps");

        public static string OffenderSteps(Language lang) =>
            MessageCatalog.Format(lang, "Roe.OffenderSteps");

        // steps: VictimSteps/OffenderSteps; diplomat: role mention or DiplomatFallback.
        public static string Instructions(Language lang, string name, string steps, string diplomat) =>
            MessageCatalog.Format(lang, "Roe.Instructions", ("name", name), ("steps", steps), ("diplomat", diplomat));

        public static string DiplomatFallback(Language lang) =>
            MessageCatalog.Format(lang, "Roe.DiplomatFallback");

        public static string ReadyButton(Language lang) =>
            MessageCatalog.Format(lang, "Roe.ReadyButton");

        public static string DoneButton(Language lang) =>
            MessageCatalog.Format(lang, "Roe.DoneButton");

        public static string ModalTitle(Language lang) =>
            MessageCatalog.Format(lang, "Roe.ModalTitle");

        public static string ModalTagLabel(Language lang) =>
            MessageCatalog.Format(lang, "Roe.ModalTagLabel");

        public static string ModalTagPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Roe.ModalTagPlaceholder");

        public static string ModalNameLabel(Language lang) =>
            MessageCatalog.Format(lang, "Roe.ModalNameLabel");

        public static string ModalNamePlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Roe.ModalNamePlaceholder");

        public static string ChannelNotConfigured(Language lang) =>
            MessageCatalog.Format(lang, "Roe.ChannelNotConfigured");

        public static string Misconfigured(Language lang) =>
            MessageCatalog.Format(lang, "Roe.Misconfigured");

        public static string CreatedTitle(Language lang) =>
            MessageCatalog.Format(lang, "Roe.CreatedTitle");

        public static string CreatedBody(Language lang, string name, string thread) =>
            MessageCatalog.Format(lang, "Roe.CreatedBody", ("name", name), ("thread", thread));

        public static string ReportNotFound(Language lang) =>
            MessageCatalog.Format(lang, "Roe.ReportNotFound");

        public static string OnlyReporterConfirm(Language lang) =>
            MessageCatalog.Format(lang, "Roe.OnlyReporterConfirm");

        public static string CaseReady(Language lang) =>
            MessageCatalog.Format(lang, "Roe.CaseReady");

        public static string SendFailed(Language lang) =>
            MessageCatalog.Format(lang, "Roe.SendFailed");

        public static string DiplomatNotified(Language lang) =>
            MessageCatalog.Format(lang, "Roe.DiplomatNotified");

        public static string OnlyReporterClose(Language lang) =>
            MessageCatalog.Format(lang, "Roe.OnlyReporterClose");

        public static string AlreadyClosed(Language lang) =>
            MessageCatalog.Format(lang, "Roe.AlreadyClosed");

        public static string CloseFailed(Language lang) =>
            MessageCatalog.Format(lang, "Roe.CloseFailed");

        public static string Closed(Language lang) =>
            MessageCatalog.Format(lang, "Roe.Closed");

        public static string UnknownReportType(Language lang) =>
            MessageCatalog.Format(lang, "Roe.UnknownReportType");







    }
}
