namespace HoshiBot.Web.Components.Shared;

// A member's own self-service pages under /me, as one list — same shape as GuildAdminPages,
// shared by the /me index card grid and the landing page's "For Members" section so the two
// can't drift apart. Unlike GuildAdminPages the description lives here: these subtitles are
// static (no live counts to resolve per page), and there's no entry for the index itself.
public sealed record MePage(
    string Label,
    string Icon,          // Open Iconic class
    string Description,
    string RouteSuffix);  // appended to me/

public static class MePages
{
    public static readonly IReadOnlyList<MePage> All =
    [
        new("My Profile", "oi-person",
            "What Hoshi knows about you — how she should address you, your languages and interests.",
            "lore"),
    ];

    public static string Href(MePage page) => $"me/{page.RouteSuffix}";
}
