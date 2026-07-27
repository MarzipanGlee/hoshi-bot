using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HoshiBot.Domain.Localization;

// The message catalog: per-locale templates embedded as Localization/Locales/{code}.json
// (flat key → template), loaded lazily. Templates use named placeholders — "{name}" or
// "{name:format}"; formats apply via the language's culture so dates/numbers localize.
// Lookup falls back language → en → the raw key and never throws at runtime — the
// MessageCatalogTests parity suite is the guard that keys/placeholders exist and match
// across every enabled locale.
public static partial class MessageCatalog
{
    private static readonly ConcurrentDictionary<Language, FrozenDictionary<string, string>> Cache = new();

    [GeneratedRegex(@"\{(\w+)(?::([^}]+))?\}")]
    private static partial Regex PlaceholderRegex();

    public static string Format(Language language, string key, params (string Name, object? Value)[] args)
    {
        var template = Lookup(language, key);
        if (args.Length == 0)
            return template;

        var culture = Languages.ToCulture(language);
        return PlaceholderRegex().Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            foreach (var (argName, value) in args)
            {
                if (argName != name)
                    continue;

                var format = match.Groups[2].Success ? match.Groups[2].Value : null;
                return value switch
                {
                    null => "",
                    IFormattable formattable => formattable.ToString(format, culture),
                    _ => value.ToString() ?? "",
                };
            }

            // Unknown token: leave it visible rather than swallowing it — a wrong
            // placeholder should be noticeable (and is caught by the parity tests).
            return match.Value;
        });
    }

    // Plural-aware variant: selects "{keyBase}.one/.few/.many/.other" via PluralRules
    // and passes the count along as the "count" argument.
    public static string FormatCount(Language language, string keyBase, int count, params (string Name, object? Value)[] args)
    {
        var form = PluralRules.Select(language, count);
        var key = $"{keyBase}.{form}";
        if (!Templates(language).ContainsKey(key) && !Templates(Language.En).ContainsKey(key))
            key = $"{keyBase}.other";

        var allArgs = new (string, object?)[args.Length + 1];
        allArgs[0] = ("count", count);
        args.CopyTo(allArgs, 1);
        return Format(language, key, allArgs);
    }

    // Exposed for the parity tests (key/placeholder comparison across locales).
    public static IReadOnlyDictionary<string, string> Templates(Language language) =>
        Cache.GetOrAdd(language, Load);

    private static string Lookup(Language language, string key)
    {
        if (Templates(language).TryGetValue(key, out var template))
            return template;

        if (language != Language.En && Templates(Language.En).TryGetValue(key, out var fallback))
            return fallback;

        return key;
    }

    private static FrozenDictionary<string, string> Load(Language language)
    {
        var resource = $"HoshiBot.Domain.Localization.Locales.{Languages.ToCode(language)}.json";
        using var stream = typeof(MessageCatalog).Assembly.GetManifestResourceStream(resource);
        if (stream is null)
            return FrozenDictionary<string, string>.Empty;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
        return entries?.ToFrozenDictionary() ?? FrozenDictionary<string, string>.Empty;
    }
}
