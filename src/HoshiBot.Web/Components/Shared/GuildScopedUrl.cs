using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Shared;

// Where to land when the guild picker switches guilds while a guild-scoped page is open. Switching
// used to refresh only the sidebar, leaving the page itself pointed at the previous guild.
//
// The rule is "keep the same page for the new guild when that page makes sense there, otherwise
// fall back to its overview": guild-level pages always carry over, audience-scoped ones only if the
// new guild serves that audience, and an alliance-scoped page moves to the new guild's own primary
// alliance (its route carries an alliance id belonging to the old guild).
//
// Pure so the decision is one readable table rather than branching inside the selector's click
// handler.
public static class GuildScopedUrl
{
    // path is the app-relative path of the open page (no leading slash), e.g.
    // "manage/guild/123/server/features/absences". Returns null when there is nothing to do —
    // the open page isn't guild-scoped at all (the dashboard, /me, an operator area), so switching
    // guilds shouldn't move the user.
    public static string? Rewrite(
        string path,
        ulong oldGuildId,
        ulong newGuildId,
        GuildAudience newGuildAudiences,
        int? newGuildPrimaryAllianceId)
    {
        // The prefix has to end on a segment boundary: "manage/guild/1" is a plain string prefix of
        // "manage/guild/10/settings", which would otherwise rewrite a different guild's page.
        var prefix = $"manage/guild/{oldGuildId}";
        var isGuildScoped = path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
        if (!isGuildScoped)
            return null;

        var newPrefix = $"manage/guild/{newGuildId}";
        var rest = path[prefix.Length..].TrimStart('/');
        if (rest.Length == 0)
            return newPrefix;

        var segments = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Alliance-scoped: /alliances, /alliance, /alliance/{id}/… . All need the Alliance audience,
        // and the id in the route belongs to the old guild, so it's replaced with the new guild's
        // primary alliance — no alliance linked there means there's no equivalent page to land on.
        var isAlliancesList = string.Equals(segments[0], "alliances", StringComparison.OrdinalIgnoreCase);
        if (isAlliancesList || string.Equals(segments[0], "alliance", StringComparison.OrdinalIgnoreCase))
        {
            if (!newGuildAudiences.HasFlag(GuildAudience.Alliance))
                return newPrefix;

            if (isAlliancesList)
                return $"{newPrefix}/alliances";

            if (newGuildPrimaryAllianceId is not { } allianceId)
                return newPrefix;

            // /alliance/{id}/rest… → keep everything after the id; /alliance/settings (no id) and
            // bare /alliance keep their own shape.
            var tail = segments.Length >= 2 && int.TryParse(segments[1], out _)
                ? string.Join('/', segments.Skip(2))
                : string.Join('/', segments.Skip(1));

            return tail.Length == 0
                ? $"{newPrefix}/alliance/{allianceId}"
                : $"{newPrefix}/alliance/{allianceId}/{tail}";
        }

        // Audience-scoped: /{slug}/settings, /{slug}/features[/…]. Only carries over when the new
        // guild serves that audience — otherwise the page would render an audience it doesn't have.
        if (AudienceDisplay.TryParseSlug(segments[0], out var audience) && audience != GuildAudience.Guild)
        {
            return newGuildAudiences.HasFlag(audience) ? $"{newPrefix}/{rest}" : newPrefix;
        }

        // Everything else is guild-level (settings, features, audience, setup-wizard,
        // permission-check) and applies to any guild.
        return $"{newPrefix}/{rest}";
    }
}
