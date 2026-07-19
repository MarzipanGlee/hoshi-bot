using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.PlayerLink;

public class PlayerLinkFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.PlayerLink;
    public string Slug => "player-link";
    public string Title => "Player Assignment";

    public string Description =>
        "Links members to their STFC player by matching Discord nicknames against the whole player " +
        "catalog — which drives rank/ops/nickname role sync. Confident matches link automatically; " +
        "everyone else you assign by hand on the member page. Never messages members.";

    public string Icon => "oi-link-intact";
    public Type EditorComponentType => typeof(PlayerLinkEditor);

    // No required settings — auto-match works from the catalog + the guild's alliances, and manual
    // assignment is always available on the member page. Enabled ⇒ configured.
    public Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        Task.FromResult(true);
}
