using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.AnnouncementForwarder;

public class AnnouncementForwarderFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.AnnouncementForwarder;
    public string Slug => "announcement-forwarder";
    public string Title => "Announcement Forwarder";

    public string Description =>
        "Auto-translates official announcements (e.g. Scopely's English STFC news) posted in the source " +
        "channels and reposts them as a branded translation in a destination channel, so members who " +
        "don't read the source language still see them.";

    public string Icon => "oi-globe";
    public Type EditorComponentType => typeof(AnnouncementForwarderEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, AnnouncementForwarderSettingKeys.Channel) is not null;
}
