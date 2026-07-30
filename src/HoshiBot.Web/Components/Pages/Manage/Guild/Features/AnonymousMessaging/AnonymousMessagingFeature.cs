using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.AnonymousMessaging;

public class AnonymousMessagingFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.AnonymousMessaging;
    public string Slug => "anonymous-messaging";

    public string Icon => "oi-envelope-closed";
    public Type EditorComponentType => typeof(AnonymousMessagingEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, AnonymousMessagingSettingKeys.Channel) is not null;
}
