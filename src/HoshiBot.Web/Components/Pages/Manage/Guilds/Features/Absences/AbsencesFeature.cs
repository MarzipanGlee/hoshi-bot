using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.Absences;

public class AbsencesFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.Absences;
    public string Slug => "absences";
    public string Title => "Absences";

    public string Description =>
        "Members can report an upcoming absence; a kept-updated report of who's away posts to a public channel " +
        "(and a staff-only copy) so leadership doesn't need to ask around.";

    public string Icon => "oi-calendar";
    public Type EditorComponentType => typeof(AbsencesEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.Settings.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, AbsencesSettingKeys.ReportChannel) is not null;
}
