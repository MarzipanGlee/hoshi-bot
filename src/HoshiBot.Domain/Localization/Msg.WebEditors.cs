namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Free-prose editor text ("Web.Editor.<Feature>.<Name>") for the per-feature Web
    // editors — intros, list headings, option labels, statuses, buttons: everything not
    // tied to one stored setting key. Card strings that DO belong to a stored setting go
    // through the dynamic Msg.WebEditor.Label/CardTitle/Usage helper instead (keyed by
    // the *SettingKeys constants). One nested class per feature that needs any.

    public static class WebAiBackend
    {
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.Intro");

        public static string ProviderOptionGemini(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.ProviderOptionGemini");

        public static string ProviderOptionOllama(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.ProviderOptionOllama");

        public static string KeySaved(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.KeySaved");

        public static string KeySavedPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.KeySavedPlaceholder");

        public static string EnterKeyPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.EnterKeyPlaceholder");

        public static string ModelLabelOllama(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.ModelLabelOllama");

        public static string ModelLabelGemini(Language lang, string model) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.ModelLabelGemini", ("model", model));

        public static string ServerDefaultModelPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.ServerDefaultModelPlaceholder");

        public static string EmbeddingOptionOllama(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.EmbeddingOptionOllama");

        public static string EmbeddingOptionGemini1(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.EmbeddingOptionGemini1");

        public static string EmbeddingOptionGemini2(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.EmbeddingOptionGemini2");

        public static string ServerDefaultPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.ServerDefaultPlaceholder");

        // Contains inline <code> markup — render via MarkupString (catalog-authored, no user input).
        public static string GateUsageOllama(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.GateUsageOllama");

        public static string DefaultModelPlaceholder(Language lang, string model) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.DefaultModelPlaceholder", ("model", model));

        // Contains inline <code> markup — render via MarkupString (model is a code constant).
        public static string GateUsageGemini(Language lang, string model) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.GateUsageGemini", ("model", model));

        // Contains inline <code> markup — render via MarkupString (model is a code constant).
        public static string RouterTip(Language lang, string model) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.RouterTip", ("model", model));

        public static string RouterOffOllamaPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.RouterOffOllamaPlaceholder");

        public static string RouterOffGeminiPlaceholder(Language lang, string model) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.RouterOffGeminiPlaceholder", ("model", model));
    }

    public static class WebAiChat
    {
        // Intro paragraphs contain inline <strong>/<em> markup — render via MarkupString.
        public static string Intro1(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.Intro1");

        public static string Intro2(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.Intro2");

        public static string HealthLink(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.HealthLink");

        public static string OpenMemoriesLink(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.OpenMemoriesLink");

        public static string ListenTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.ListenTitle");

        public static string ListenUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.ListenUsage");

        public static string KnowledgePreferredTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.KnowledgePreferredTitle");

        public static string KnowledgePreferredUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.KnowledgePreferredUsage");

        public static string KnowledgeNormalTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.KnowledgeNormalTitle");

        public static string KnowledgeNormalUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.KnowledgeNormalUsage");

        public static string KnowledgeLastResortTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.KnowledgeLastResortTitle");

        public static string KnowledgeLastResortUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.KnowledgeLastResortUsage");
    }

    public static class WebAllianceTournament
    {
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTournament.Intro");
    }

    public static class WebInfiniteIncursions
    {
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.InfiniteIncursions.Intro");
    }

    public static class WebAnnouncementForwarder
    {
        public static string SourcesTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AnnouncementForwarder.SourcesTitle");

        public static string SourcesUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AnnouncementForwarder.SourcesUsage");

        public static string ServerLanguageOption(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AnnouncementForwarder.ServerLanguageOption");
    }

    public static class WebClientRelease
    {
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ClientRelease.Intro");

        public static string ChannelsTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ClientRelease.ChannelsTitle");

        public static string ChannelsUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ClientRelease.ChannelsUsage");

        public static string PlatformRolesTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ClientRelease.PlatformRolesTitle");

        public static string PlatformRolesUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ClientRelease.PlatformRolesUsage");
    }

    public static class WebCommandBridge
    {
        // Contains inline <strong> markup — render via MarkupString.
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.Intro");

        public static string UserBridgeHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.UserBridgeHeading");

        public static string UserBridgeSubtitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.UserBridgeSubtitle");

        public static string StaffBridgeHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.StaffBridgeHeading");

        public static string StaffBridgeSubtitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.StaffBridgeSubtitle");

        public static string FriendsBridgeHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.FriendsBridgeHeading");

        public static string FriendsBridgeSubtitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.FriendsBridgeSubtitle");

        public static string ChannelUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.ChannelUsage");

        public static string Publish(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.Publish");

        public static string StatusQueued(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.StatusQueued");

        public static string StatusPosted(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.StatusPosted");

        public static string StatusStillQueued(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.StatusStillQueued");

        public static string ButtonsHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.ButtonsHeading");

        public static string NoButtons(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.NoButtons");

        public static string Hidden(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.Hidden");
    }
}
