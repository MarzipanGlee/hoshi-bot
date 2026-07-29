using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Web admin display text for features ("Web.Feature.<GuildFeature>.*") — the
    // noun-phrased admin-UI wording, deliberately separate from the action-phrased
    // Discord-facing "Feature.<X>" labels (e.g. "Absences" vs "Manage absences").
    public static class WebFeature
    {
        public static string Title(Language lang, GuildFeature feature) =>
            Fallback(MessageCatalog.Format(lang, $"Web.Feature.{feature}.Title"), $"Web.Feature.{feature}.Title", feature.ToString());

        public static string Description(Language lang, GuildFeature feature) =>
            Fallback(MessageCatalog.Format(lang, $"Web.Feature.{feature}.Description"), $"Web.Feature.{feature}.Description", "");

        // A feature's extra admin page ("Web.Feature.<GuildFeature>.Extra.<Slug>"),
        // e.g. Member Lore's "Interviews" page. Slug is the route slug.
        public static string ExtraTitle(Language lang, GuildFeature feature, string slug) =>
            Fallback(MessageCatalog.Format(lang, $"Web.Feature.{feature}.Extra.{slug}"), $"Web.Feature.{feature}.Extra.{slug}", slug);

        // Enum-driven keys have no compile check — an unmapped value falls back rather
        // than leaking a raw catalog key into the UI.
        private static string Fallback(string formatted, string key, string fallback) =>
            formatted == key ? fallback : formatted;
    }

    // Web admin display text for audiences ("Web.Audience.<GuildAudience>.*"),
    // backing AudienceDisplay — separate from the Discord-facing "Audience.<X>" keys.
    public static class WebAudience
    {
        public static string Label(Language lang, GuildAudience audience)
        {
            var key = $"Web.Audience.{audience}.Label";
            var label = MessageCatalog.Format(lang, key);
            return label == key ? audience.ToString() : label;
        }

        public static string Description(Language lang, GuildAudience audience)
        {
            var key = $"Web.Audience.{audience}.Description";
            var description = MessageCatalog.Format(lang, key);
            return description == key ? "" : description;
        }
    }

    // Per-setting editor card text ("Web.Editor.<GuildFeature>.<SettingKey>.*"), keyed
    // by the same *SettingKeys storage constants the editors already pass to
    // GuildFeatureSettingsService — reusing them keeps key typos structurally unlikely.
    public static class WebEditor
    {
        public static string Label(Language lang, GuildFeature feature, string settingKey) =>
            MessageCatalog.Format(lang, $"Web.Editor.{feature}.{settingKey}.Label");

        public static string CardTitle(Language lang, GuildFeature feature, string settingKey) =>
            MessageCatalog.Format(lang, $"Web.Editor.{feature}.{settingKey}.CardTitle");

        public static string Usage(Language lang, GuildFeature feature, string settingKey) =>
            MessageCatalog.Format(lang, $"Web.Editor.{feature}.{settingKey}.Usage");
    }
}
