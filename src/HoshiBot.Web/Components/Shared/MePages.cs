namespace HoshiBot.Web.Components.Shared;

// A member's own self-service pages under /me, as one list — shared by the /me index card grid
// and the landing page's "For Members" section so the two can't drift apart. Unlike
// GuildAdminPages the description lives here: these subtitles are static (no live counts to
// resolve per page), and there's no entry for the index itself.
public static class MePages
{
    public static readonly IReadOnlyList<AdminPage> All =
    [
        new("Web.Page.Me.Lore", "lore", Icon: "oi-person",
            DescriptionKey: "Web.Page.Me.Lore.Description"),
    ];

    public static string Href(AdminPage page) => page.Href("me");
}
