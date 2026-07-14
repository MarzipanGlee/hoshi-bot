using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.ClientRelease;

public class ClientReleaseFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.ClientRelease;
    public string Slug => "client-release";
    public string Title => "Client Release Announcements";

    public string Description =>
        "Announces when a new STFC game client version is released (Windows, macOS, Android, iOS) — relevant " +
        "to any guild, not just alliances.";

    public string Icon => "oi-cloud-download";
    public Type EditorComponentType => typeof(ClientReleaseEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.GuildAlertChannels.AnyAsync(c => c.GuildId == guildId && c.Kind == GuildAlertChannelKind.ClientRelease && c.Audience == audience);
    }
}
