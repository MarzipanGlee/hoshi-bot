namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Announcements: the publish flow (AnnouncementService + AnnouncementButtonModule's
    // audience/severity preview and read-receipt buttons), the read-count refresh job,
    // and the auto-translation forwarder (AnnouncementForwarderService).
    public static class Announce
    {
        public static string ReadButton(Language lang, int count) =>
            MessageCatalog.Format(lang, "Announce.ReadButton", ("count", count));

        // Publish result when no channel is configured (previously English; German
        // authored per plan decision U4).
        public static string ChannelNotConfigured(Language lang) =>
            MessageCatalog.Format(lang, "Announce.ChannelNotConfigured");

        public static string FieldSeverity(Language lang) =>
            MessageCatalog.Format(lang, "Announce.FieldSeverity");

        public static string FieldOnBehalfOf(Language lang) =>
            MessageCatalog.Format(lang, "Announce.FieldOnBehalfOf");

        // Fallback attribution when no Command Staff role is configured/resolvable.
        public static string AttributionFallback(Language lang) =>
            MessageCatalog.Format(lang, "Announce.AttributionFallback");

        public static string FieldAttachments(Language lang) =>
            MessageCatalog.Format(lang, "Announce.FieldAttachments");

        public static string AttachmentLink(Language lang, int number, string url) =>
            MessageCatalog.Format(lang, "Announce.AttachmentLink", ("number", number), ("url", url));

        // Publish success result (previously English; German authored per plan decision U4).
        public static string Published(Language lang, string channel) =>
            MessageCatalog.Format(lang, "Announce.Published", ("channel", channel));

        // Severity names double as the embed's severity-field value and the publish-button
        // labels (Direct only appears as a button; its published embed shows Normal, as legacy did).
        public static string SeverityNormal(Language lang) =>
            MessageCatalog.Format(lang, "Announce.SeverityNormal");

        public static string SeverityElevated(Language lang) =>
            MessageCatalog.Format(lang, "Announce.SeverityElevated");

        public static string SeverityHigh(Language lang) =>
            MessageCatalog.Format(lang, "Announce.SeverityHigh");

        public static string SeverityDirect(Language lang) =>
            MessageCatalog.Format(lang, "Announce.SeverityDirect");

        // The preview wizard (AnnouncementButtonModule).
        public static string Discarded(Language lang) =>
            MessageCatalog.Format(lang, "Announce.Discarded");

        public static string AudiencePrompt(Language lang) =>
            MessageCatalog.Format(lang, "Announce.AudiencePrompt");

        // {severity} is the emoji + label of the reaction that opened the prompt.
        public static string PublishPrompt(Language lang, string severity) =>
            MessageCatalog.Format(lang, "Announce.PublishPrompt", ("severity", severity));

        public static string PublishButton(Language lang) =>
            MessageCatalog.Format(lang, "Announce.PublishButton");

        public static string CancelButton(Language lang) =>
            MessageCatalog.Format(lang, "Announce.CancelButton");

        public static string NoTitle(Language lang) =>
            MessageCatalog.Format(lang, "Announce.NoTitle");

        public static string NoBody(Language lang) =>
            MessageCatalog.Format(lang, "Announce.NoBody");

        public static string AttachmentCount(Language lang, int count) =>
            MessageCatalog.Format(lang, "Announce.AttachmentCount", ("count", count));

        // The personal read-receipt ack.
        public static string ReadRecorded(Language lang) =>
            MessageCatalog.Format(lang, "Announce.ReadRecorded");

        public static string AlreadyRead(Language lang) =>
            MessageCatalog.Format(lang, "Announce.AlreadyRead");

        // AnnouncementCounterRefreshJob's admin-notify action/hint pieces.
        public static string ActionUpdate(Language lang) =>
            MessageCatalog.Format(lang, "Announce.ActionUpdate");

        public static string HintChannelPermission(Language lang, string channel) =>
            MessageCatalog.Format(lang, "Announce.HintChannelPermission", ("channel", channel));

        // The forwarded-translation embed (AnnouncementForwarderService).
        public static string ForwardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Announce.ForwardTitle");

        public static string ForwardFieldOriginal(Language lang) =>
            MessageCatalog.Format(lang, "Announce.ForwardFieldOriginal");

        public static string ForwardOriginalLink(Language lang, string link) =>
            MessageCatalog.Format(lang, "Announce.ForwardOriginalLink", ("link", link));

        public static string ForwardFieldUpdated(Language lang) =>
            MessageCatalog.Format(lang, "Announce.ForwardFieldUpdated");
    }
}
