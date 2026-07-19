using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.MemberOnboarding;

public class MemberOnboardingFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.MemberOnboarding;
    public string Slug => "member-onboarding";
    public string Title => "Member Onboarding";

    public string Description =>
        "Opt-in: DMs members that Player Assignment couldn't place automatically, asking them to confirm the " +
        "bot's best guess or type their in-game player. Off by default — leave it off to resolve missing " +
        "assignments only via Player Assignment's admin table, without ever DMing members.";

    public string Icon => "oi-envelope-closed";
    public Type EditorComponentType => typeof(MemberOnboardingEditor);

    // No required settings — it just needs turning on (and Player Assignment enabled to produce the
    // unresolved members it reaches out to). Enabled ⇒ configured.
    public Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        Task.FromResult(true);
}
