using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Audience display labels ("Audience.<GuildAudience>"), backing
    // GuildFeatureService.AudienceLabel.
    public static class Audience
    {
        public static string Label(Language lang, GuildAudience audience)
        {
            // Enum-driven key. An audience without a catalog entry (e.g. None) falls back
            // to its enum name — the old switch's default arm.
            var key = $"Audience.{audience}";
            var label = MessageCatalog.Format(lang, key);
            return label == key ? audience.ToString() : label;
        }
    }
}
