using System.Net;
using System.Text.RegularExpressions;

namespace HoshiBot.StfcCatalogSync;

public record ParsedServerCard(string Name, string Region, int Number, string? VeilGroupCode, string? InviteUrl);

// Parses a static HTML snapshot of stfc.pro's server-overview page (manually saved by a
// human — see hoshi-bot-dotnet/data/, never fetched live). Coupled to that page's current
// CSS class names, so it'll need updating if the site's markup changes; acceptable for a
// manually-triggered, occasionally-rerun dev tool rather than hardened production code.
public static partial class StfcCatalogHtmlParser
{
    [GeneratedRegex(@"<div[^>]*data-slot=""card""[^>]*>")]
    private static partial Regex CardOpenRegex();

    [GeneratedRegex(@"<div[^>]*>|</div>")]
    private static partial Regex DivTagRegex();

    [GeneratedRegex(@"text-xl"">([^<]+)</span>")]
    private static partial Regex NameRegex();

    // Every server card shows its region + number as e.g. "US<!-- --> <!-- -->8" — the
    // <!-- --> comments (React hydration whitespace markers) split what renders as "US 8".
    [GeneratedRegex(@"text-blue-400 text-xl"">([A-Za-z]+)<!-- --> <!-- -->(\d+)</span>")]
    private static partial Regex RegionNumberRegex();

    [GeneratedRegex(@"text-white/70"">([^<]+)</span>")]
    private static partial Regex BadgeRegex();

    // Not every card has one — confirmed only ~97 of ~113 server cards carry this anchor.
    [GeneratedRegex(@"aria-label=""Open server Discord""[^>]*href=""([^""]+)""")]
    private static partial Regex InviteUrlRegex();

    public static List<ParsedServerCard> Parse(string html)
    {
        var results = new List<ParsedServerCard>();

        foreach (Match cardOpen in CardOpenRegex().Matches(html))
        {
            var cardHtml = ExtractBalancedDiv(html, cardOpen.Index, cardOpen.Index + cardOpen.Length);
            if (cardHtml is null)
                continue;

            var nameMatch = NameRegex().Match(cardHtml);
            var regionNumberMatch = RegionNumberRegex().Match(cardHtml);
            if (!nameMatch.Success || !regionNumberMatch.Success)
                continue; // not a server card (e.g. a summary stats card)

            var name = WebUtility.HtmlDecode(nameMatch.Groups[1].Value);
            var region = regionNumberMatch.Groups[1].Value;
            var number = int.Parse(regionNumberMatch.Groups[2].Value);
            var badgeMatch = BadgeRegex().Match(cardHtml);
            var veilGroupCode = badgeMatch.Success ? badgeMatch.Groups[1].Value : null;
            var inviteUrlMatch = InviteUrlRegex().Match(cardHtml);
            var inviteUrl = inviteUrlMatch.Success ? WebUtility.HtmlDecode(inviteUrlMatch.Groups[1].Value) : null;

            results.Add(new ParsedServerCard(name, region, number, veilGroupCode, inviteUrl));
        }

        return results;
    }

    // Scans forward from a <div data-slot="card" ...> tag's end, tracking nested div depth,
    // to find its matching closing </div> — scoping name/badge extraction to exactly one
    // card's content instead of a fragile "nearest text within N characters" heuristic
    // (which mis-associated cards near section boundaries during development).
    private static string? ExtractBalancedDiv(string html, int start, int contentStart)
    {
        var depth = 1;
        var pos = contentStart;
        while (depth > 0)
        {
            var tagMatch = DivTagRegex().Match(html, pos);
            if (!tagMatch.Success)
                return null;

            depth += tagMatch.Value.StartsWith("</div", StringComparison.Ordinal) ? -1 : 1;
            pos = tagMatch.Index + tagMatch.Length;
        }

        return html[start..pos];
    }
}
