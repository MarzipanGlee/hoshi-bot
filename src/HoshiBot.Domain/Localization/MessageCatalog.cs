using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
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
        // Icon placeholders need resolving even with no caller arguments, so this can't take the
        // old "no args, no work" shortcut; the cheap check is whether there's a placeholder at all.
        if (!template.Contains('{'))
            return template;

        var culture = Languages.ToCulture(language);
        return PlaceholderRegex().Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            if (name == IconPlaceholder && match.Groups[2].Success && ResolveIcon(match.Groups[2].Value) is { } icon)
                return icon;

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

    // "{icon:Name}" pulls from the Icons registry — including the nested Text group, addressed as
    // "{icon:Text.Check}". Returns null for an unknown name so the token stays visible in the output
    // rather than silently rendering as nothing; MessageCatalogTests fails the build on one.
    private const string IconPlaceholder = "icon";

    public static string? ResolveIcon(string name) => IconsByName.GetValueOrDefault(name);

    // Every public string constant on Icons, keyed by "Name" or "Group.Name".
    public static readonly FrozenDictionary<string, string> IconsByName = CollectIcons(typeof(Icons), "")
        .ToFrozenDictionary();

    private static IEnumerable<KeyValuePair<string, string>> CollectIcons(Type type, string prefix)
    {
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field is { IsLiteral: true, FieldType: { } t } && t == typeof(string) && field.GetRawConstantValue() is string value)
                yield return new KeyValuePair<string, string>(prefix + field.Name, value);
        }

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
        {
            foreach (var entry in CollectIcons(nested, $"{prefix}{nested.Name}."))
                yield return entry;
        }
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

    // A locale is split across two embedded files: the bot-facing catalog
    // (Locales/{code}.json) and the Web admin's (Locales/Web/{code}.json, all keys
    // "Web."-prefixed). Merged into one lookup here; MessageCatalogTests enforces that
    // the two never share a key, so merge order can't matter.
    private static FrozenDictionary<string, string> Load(Language language)
    {
        var code = Languages.ToCode(language);
        var entries = new Dictionary<string, string>();
        foreach (var resource in new[] { $"Locales.{code}.json", $"Locales.Web.{code}.json" })
        {
            using var stream = typeof(MessageCatalog).Assembly
                .GetManifestResourceStream($"HoshiBot.Domain.Localization.{resource}");
            if (stream is null)
                continue;

            using var reader = new StreamReader(stream, Encoding.UTF8);
            foreach (var (key, template) in JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd()) ?? [])
                entries[key] = template;
        }

        return entries.ToFrozenDictionary();
    }
}
