using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.ClientRelease;

public class ClientReleaseFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.ClientRelease;
    public string Slug => "client-release";

    public string Icon => "oi-cloud-download";
    public Type EditorComponentType => typeof(ClientReleaseEditor);

    public Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        context.HasFeatureChannelAsync(guildId, GuildFeature.ClientRelease, audience);
}
