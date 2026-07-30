using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.MemberOnboarding;

public class MemberOnboardingFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.MemberOnboarding;
    public string Slug => "member-onboarding";

    public string Icon => "oi-envelope-closed";
    public Type EditorComponentType => typeof(MemberOnboardingEditor);

    // No required settings — it just needs turning on (and Player Assignment enabled to produce the
    // unresolved members it reaches out to). Enabled ⇒ configured.
    public Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        Task.FromResult(true);
}
