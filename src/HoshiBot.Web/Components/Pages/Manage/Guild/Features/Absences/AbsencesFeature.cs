using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.Absences;

public class AbsencesFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.Absences;
    public string Slug => "absences";

    public string Icon => "oi-calendar";
    public Type EditorComponentType => typeof(AbsencesEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, AbsencesSettingKeys.ReportChannel) is not null;
}
