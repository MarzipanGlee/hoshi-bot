using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.PlayerLink;

public class PlayerLinkFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.PlayerLink;
    public string Slug => "player-link";

    public string Icon => "oi-link-intact";
    public Type EditorComponentType => typeof(PlayerLinkEditor);

    public IReadOnlyList<FeatureExtraPage> ExtraPages =>
        [new FeatureExtraPage("assignments", typeof(PlayerAssignmentsAdmin))];

    // No required settings — auto-match works from the catalog + the guild's alliances, and manual
    // assignment is always available on the member page. Enabled ⇒ configured.
    public Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        Task.FromResult(true);
}
