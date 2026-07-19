using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.NicknameSync;

public class NicknameSyncFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.NicknameSync;
    public string Slug => "nickname-sync";
    public string Title => "Nickname Sync";

    public string Description =>
        "Renames members' Discord nicknames to match their linked in-game player, optionally prefixing a " +
        "server and/or alliance tag (e.g. [EU164][XYZ] Name) to disambiguate foreign players.";

    public string Icon => "oi-tag";
    public Type EditorComponentType => typeof(NicknameSyncEditor);

    // No required settings — the tag modes default to Foreign only, so enabling is enough.
    public Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        Task.FromResult(true);
}
