using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.HoshiSay;

public class HoshiSayFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.HoshiSay;
    public string Slug => "hoshi-say";
    public string Title => "Hoshi Say";

    public string Description =>
        "Lets an admin have Hoshi compose a message in her own voice (via the AI backend) and post it as " +
        "a plain chat line into a chosen channel — e.g. to comfort or address a member. The /hoshi-say " +
        "command runs only from the configured trigger channel(s), so channel access is the permission gate.";

    public string Icon => "oi-comment-square";
    public Type EditorComponentType => typeof(HoshiSayEditor);

    // Configured once at least one trigger channel is set (the channel(s) the command may run from).
    public Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        context.HasFeatureChannelAsync(guildId, Feature, audience);
}
