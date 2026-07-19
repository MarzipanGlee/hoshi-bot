using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Shared;

public static class AudienceDisplay
{
    public static IEnumerable<string> Labels(GuildAudience audiences)
    {
        if (audiences.HasFlag(GuildAudience.Guild))
            yield return "Guild";
        if (audiences.HasFlag(GuildAudience.Alliance))
            yield return "Alliance";
        if (audiences.HasFlag(GuildAudience.Server))
            yield return "Server";
        if (audiences.HasFlag(GuildAudience.VeilGroup))
            yield return "Veil Group";
        if (audiences.HasFlag(GuildAudience.Community))
            yield return "Community";
    }

    // Shared between AudienceEditor's cards and (formerly) the plain-checkbox labels it replaced.
    public static string Description(GuildAudience audience) => audience switch
    {
        GuildAudience.Guild => "Guild-wide features that apply to the whole Discord, regardless of audience.",
        GuildAudience.Alliance => "This guild is a single alliance's own Discord.",
        GuildAudience.Server => "This guild serves a whole STFC server.",
        GuildAudience.VeilGroup => "This guild serves a veil group (a coalition of servers).",
        GuildAudience.Community => "A general Discord not tied to one alliance or server.",
        _ => "",
    };

    // Open Iconic class for a single audience flag — shared between the Features page's
    // section headers and the public landing page's audience overview cards, so both stay
    // in sync if an icon ever changes.
    public static string Icon(GuildAudience audience) => audience switch
    {
        GuildAudience.Guild => "oi-home",
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
        GuildAudience.Guild => "guild",
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
