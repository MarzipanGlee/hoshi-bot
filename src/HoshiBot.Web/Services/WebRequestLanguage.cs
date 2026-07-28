using System.Globalization;
using System.Security.Claims;
using HoshiBot.Data;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Services;

// The language Web pages render catalog content in, resolved per request/circuit (see
// docs/localization-plan.md "Web request language"): the signed-in user's explicit
// DiscordUser.Language → the browser's Accept-Language header (first enabled match in
// q-order) → English. Read-only — it never writes anything — and independent of the
// Discord-side resolver chains (no DiscordLocale / guild fallbacks here; anonymous
// pages simply go straight to the header).
//
// Blazor Server specifics: with this app's render mode (InteractiveServer, prerender
// off) components run on the circuit, whose HttpContext is the WebSocket-upgrade
// request — cookie auth has run on it and browsers send Accept-Language on it, so both
// inputs are available via IHttpContextAccessor. The resolve is lazy and cached per
// scoped instance (one circuit), so the DB is hit at most once per circuit.
public class WebRequestLanguage(IHttpContextAccessor httpContextAccessor, IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    private Task<Language>? resolved;

    public Task<Language> GetAsync() => resolved ??= ResolveAsync();

    private async Task<Language> ResolveAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return Language.En;

        if (ulong.TryParse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var explicitCode = await db.DiscordUsers.AsNoTracking()
                .Where(u => u.DiscordUserId == userId)
                .Select(u => u.Language)
                .FirstOrDefaultAsync();
            if (Languages.Parse(explicitCode) is { } explicitLanguage)
                return explicitLanguage;
        }

        return FromAcceptLanguage(httpContext.Request.Headers.AcceptLanguage.ToString()) ?? Language.En;
    }

    // First enabled language in the header's q-order (ties keep header order); malformed
    // parts are skipped rather than failing the whole header.
    internal static Language? FromAcceptLanguage(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return null;

        return header.Split(',')
            .Select(part =>
            {
                var pieces = part.Split(';');
                var quality = 1.0;
                foreach (var parameter in pieces.Skip(1))
                {
                    var trimmed = parameter.Trim();
                    if (trimmed.StartsWith("q=", StringComparison.OrdinalIgnoreCase)
                        && double.TryParse(trimmed[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                        quality = parsed;
                }

                return (Tag: pieces[0].Trim(), Quality: quality);
            })
            .Where(candidate => candidate.Quality > 0)
            .OrderByDescending(candidate => candidate.Quality)
            .Select(candidate => Languages.FromDiscordLocale(candidate.Tag))
            .FirstOrDefault(language => language is { } match && Languages.Enabled.Contains(match));
    }
}
