using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Shared;

public static class AudienceDisplay
{
    public static IEnumerable<string> Labels(GuildAudience audiences)
    {
        if (audiences.HasFlag(GuildAudience.Alliance))
            yield return "Alliance";
        if (audiences.HasFlag(GuildAudience.Server))
            yield return "Server";
        if (audiences.HasFlag(GuildAudience.VeilGroup))
            yield return "Veil Group";
        if (audiences.HasFlag(GuildAudience.Community))
            yield return "Community";
    }

    // Open Iconic class for a single audience flag — shared between the Dashboard's section
    // headers and the public landing page's audience overview cards, so both stay in sync
    // if an icon ever changes.
    public static string Icon(GuildAudience audience) => audience switch
    {
        GuildAudience.Alliance => "oi-flag",
        GuildAudience.Server => "oi-hard-drive",
        GuildAudience.VeilGroup => "oi-layers",
        GuildAudience.Community => "oi-people",
        _ => "oi-tag",
    };

    // Kebab-case route segment for a single audience flag — lets a multi-audience feature's
    // settings route (manage/guilds/{id}/features/{slug}/{audience}) address one audience at
    // a time instead of showing every audience stacked on one page.
    public static string Slug(GuildAudience audience) => audience switch
    {
        GuildAudience.Alliance => "alliance",
        GuildAudience.Server => "server",
        GuildAudience.VeilGroup => "veil-group",
        GuildAudience.Community => "community",
        _ => "",
    };

    // Ordinal-ignore-case since this parses a route segment a user may have typed/bookmarked.
    public static bool TryParseSlug(string? slug, out GuildAudience audience)
    {
        foreach (var candidate in Enum.GetValues<GuildAudience>())
        {
            if (candidate != GuildAudience.None && string.Equals(Slug(candidate), slug, StringComparison.OrdinalIgnoreCase))
            {
                audience = candidate;
                return true;
            }
        }

        audience = GuildAudience.None;
        return false;
    }
}
