using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.MemberLore;

public class MemberLoreFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.MemberLore;
    public string Slug => "member-lore";

    public string Icon => "oi-people";
    public Type EditorComponentType => typeof(MemberLoreEditor);

    public IReadOnlyList<FeatureExtraPage> ExtraPages =>
        [
            new FeatureExtraPage("notes", typeof(MemberNotesAdmin)),
            new FeatureExtraPage("interviews", typeof(MemberInterviewsAdmin)),
        ];

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, MemberLoreSettingKeys.MemberRole) is not null;
}
