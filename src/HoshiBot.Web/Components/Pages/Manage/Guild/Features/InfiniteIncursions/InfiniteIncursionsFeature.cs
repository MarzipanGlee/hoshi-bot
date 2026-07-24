using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.InfiniteIncursions;

public class InfiniteIncursionsFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.InfiniteIncursions;
    public string Slug => "infinite-incursions";
    public string Title => "Infinite Incursions Announcements";

    public string Description =>
        "Advance-warning announcement when a new Infinite Incursions event is scheduled.";

    public string Icon => "oi-warning";
    public Type EditorComponentType => typeof(InfiniteIncursionsEditor);

    public Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        context.HasAlertChannelAsync(guildId, GuildAlertChannelKind.InfiniteIncursions, audience);
}
