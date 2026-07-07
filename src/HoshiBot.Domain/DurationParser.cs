using System.Text.RegularExpressions;

namespace HoshiBot.Domain;

// Parses compound duration strings in the legacy shield-reminder modal's format
// ("2d 3h 45m", "2d3h45m", "90m") into a TimeSpan. Returns null on unparseable input.
public static partial class DurationParser
{
    [GeneratedRegex(@"(\d+)\s*([dhm])", RegexOptions.IgnoreCase)]
    private static partial Regex ComponentRegex();

    public static TimeSpan? Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var matches = ComponentRegex().Matches(input);
        if (matches.Count == 0)
            return null;

        // Anything left after stripping the matched components (besides whitespace
        // between them) means the input contained garbage, e.g. "2x3h".
        if (!string.IsNullOrWhiteSpace(ComponentRegex().Replace(input, "")))
            return null;

        var total = TimeSpan.Zero;
        foreach (Match match in matches)
        {
            var value = int.Parse(match.Groups[1].Value);
            total += match.Groups[2].Value.ToLowerInvariant() switch
            {
                "d" => TimeSpan.FromDays(value),
                "h" => TimeSpan.FromHours(value),
                "m" => TimeSpan.FromMinutes(value),
                _ => TimeSpan.Zero,
            };
        }

        return total > TimeSpan.Zero ? total : null;
    }
}
