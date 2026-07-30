using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.RoeViolationReports;

public class RoeViolationReportsFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.RoeViolationReports;
    public string Slug => "roe-violation-reports";

    public string Icon => "oi-ban";
    public Type EditorComponentType => typeof(RoeViolationReportsEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, RoeViolationReportsSettingKeys.Channel) is not null;
}
