using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.Diplomacy;

public class DiplomacyFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.Diplomacy;
    public string Slug => "diplomacy";
    public string Title => "Diplomacy";

    public string Description =>
        "Tracks and announces this alliance's diplomatic status toward other alliances via /set-diplomacy.";

    public string Icon => "oi-people";
    public Type EditorComponentType => typeof(DiplomacyEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.Settings.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, DiplomacySettingKeys.Channel) is not null;
}
