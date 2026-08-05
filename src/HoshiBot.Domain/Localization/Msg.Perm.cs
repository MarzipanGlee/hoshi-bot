using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Discord permission names, shared vocabulary now that the bot names them in its failure
    // reports and not just the Web permission page. Lives in the bot catalog ("Perm.*") rather
    // than the Web one because both read it; the no-overlap rule between the two catalogs is about
    // key namespaces, not about which project consumes them.
    public static class Perm
    {
        // "Perm.<BotPermission>". Enum-keyed, so a missing key falls back to the enum name rather
        // than leaking a raw catalog key — BotActionCatalogTests asserts every value has one.
        public static string Name(Language lang, BotPermission permission)
        {
            var key = $"Perm.{permission}";
            var label = MessageCatalog.Format(lang, key);
            return label == key ? permission.ToString() : label;
        }

        // Every set bit, comma-separated. A [Flags] value with several bits is the normal case
        // here — "Create Posts, Send Messages in Posts".
        public static string List(Language lang, BotPermission permissions) =>
            permissions == BotPermission.None
                ? "—"
                : string.Join(", ", Enum.GetValues<BotPermission>()
                    .Where(p => p != BotPermission.None && permissions.HasFlag(p))
                    .Select(p => Name(lang, p)));
    }
}
