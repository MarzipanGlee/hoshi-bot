using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.MemberLore;

public class MemberLoreFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.MemberLore;
    public string Slug => "member-lore";
    public string Title => "Member Lore";

    public string Description =>
        "The bot DM-interviews members (optional, opt-out) to learn who they are — how to address them, " +
        "what they're into, funny stories about each other — so it can chat like a real member of the community.";

    public string Icon => "oi-people";
    public Type EditorComponentType => typeof(MemberLoreEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.Settings.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, MemberLoreSettingKeys.MemberRole) is not null;
}
