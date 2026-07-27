namespace HoshiBot.Domain.Localization;

// Typed accessors over MessageCatalog — one nested class per feature, one method per
// message, so call sites are compile-checked (wrong name/arity fails the build) and
// translators only ever touch the Locales/*.json files. Grows per feature during the
// catalog-extraction sub-phase (docs/localization-plan.md, 6d).
public static partial class Msg
{
    public static class Common
    {
        public static string Processing(Language lang) =>
            MessageCatalog.Format(lang, "Common.Processing");
    }
}
