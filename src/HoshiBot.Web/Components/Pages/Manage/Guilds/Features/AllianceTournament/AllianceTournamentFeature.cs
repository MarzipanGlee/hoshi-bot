using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.AllianceTournament;

public class AllianceTournamentFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.AllianceTournament;
    public string Slug => "alliance-tournament";
    public string Title => "Alliance Tournament Announcements";

    public string Description =>
        "Advance-warning announcement when a new Alliance Tournament event is scheduled.";

    public string Icon => "oi-flag";
    public Type EditorComponentType => typeof(AllianceTournamentEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, FeatureModuleContext context)
    {
        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.GuildAlertChannels.AnyAsync(c => c.GuildId == guildId && c.Kind == GuildAlertChannelKind.AllianceTournament && c.Audience == audience);
    }
}
