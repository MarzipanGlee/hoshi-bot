using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.RoeViolationReports;

public class RoeViolationReportsFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.RoeViolationReports;
    public string Slug => "roe-violation-reports";
    public string Title => "RoE Violation Reports";

    public string Description =>
        "Members can report a suspected Rules of Engagement violation for staff to review.";

    public string Icon => "oi-ban";
    public Type EditorComponentType => typeof(RoeViolationReportsEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.Settings.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, RoeViolationReportsSettingKeys.Channel) is not null;
}
