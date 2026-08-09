using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.NotificationOptIn;

public class NotificationOptInFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.NotificationOptIn;
    public string Slug => "notification-opt-in";

    public string Icon => "oi-bell";
    public Type EditorComponentType => typeof(NotificationOptInEditor);

    // Configured once the alliance has an alert role — the menu's own content comes from the other
    // features, so there is nothing of its own left to check.
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.GetAllianceRoleAsync(guildId, audience, guildAllianceId, a => a.AlertRoleId) is not null;
}
