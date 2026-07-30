using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.NicknameSync;

public class NicknameSyncFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.NicknameSync;
    public string Slug => "nickname-sync";

    public string Icon => "oi-tag";
    public Type EditorComponentType => typeof(NicknameSyncEditor);

    // No required settings — the tag modes default to Foreign only, so enabling is enough.
    public Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        Task.FromResult(true);
}
