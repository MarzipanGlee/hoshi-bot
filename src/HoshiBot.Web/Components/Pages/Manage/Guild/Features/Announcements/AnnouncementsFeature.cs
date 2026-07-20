using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.Announcements;

public class AnnouncementsFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.Announcements;
    public string Slug => "announcements";
    public string Title => "Announcements";

    public string Description =>
        "Staff-published announcements with read tracking and unread reminders — usable by any audience, not " +
        "alliance-specific.";

    public string Icon => "oi-bullhorn";
    public Type EditorComponentType => typeof(AnnouncementsEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.Settings.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, AnnouncementsSettingKeys.Channel) is not null;
}
