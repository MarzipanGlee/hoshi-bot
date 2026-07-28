using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Feature display labels ("Feature.<GuildFeature>") + the shared "feature disabled"
    // guard message, backing GuildFeatureService.FeatureLabel/DisabledMessage.
    public static class Feature
    {
        public static string Label(Language lang, GuildFeature feature)
        {
            // Enum-driven key. A feature without a catalog entry (e.g. the storage-only
            // buckets or CommandBridge itself) falls back to its enum name — the old
            // switch's default arm.
            var key = $"Feature.{feature}";
            var label = MessageCatalog.Format(lang, key);
            return label == key ? feature.ToString() : label;
        }

        public static string Disabled(Language lang, GuildFeature feature) =>
            MessageCatalog.Format(lang, "Feature.Disabled", ("label", Label(lang, feature)));
    }
}
