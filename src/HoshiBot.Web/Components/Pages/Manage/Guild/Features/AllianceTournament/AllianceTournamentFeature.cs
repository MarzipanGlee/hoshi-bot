using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.AllianceTournament;

public class AllianceTournamentFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.AllianceTournament;
    public string Slug => "alliance-tournament";

    public string Icon => "oi-flag";
    public Type EditorComponentType => typeof(AllianceTournamentEditor);

    public Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        context.HasAlertChannelAsync(guildId, GuildAlertChannelKind.AllianceTournament, audience);
}
