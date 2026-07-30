using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.AiChat;

public class AiChatFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.AiChat;
    public string Slug => "ai-chat";

    public string Icon => "oi-chat";
    public Type EditorComponentType => typeof(AiChatEditor);

    public IReadOnlyList<FeatureExtraPage> ExtraPages =>
    [
        new FeatureExtraPage("memories", typeof(MemoryAdmin)),
        new FeatureExtraPage("health", typeof(AiChatHealth)),
    ];

    // The AI provider/key/model now live in the guild-wide AiBackend feature (declared as a
    // dependency, so its "not configured" state surfaces via the dependency badge). AiChat's own
    // "configured" signal is therefore about having at least one listen channel to answer in for
    // this audience — enabled but with no listen channel does nothing.
    public Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        context.HasFeatureChannelAsync(guildId, GuildFeature.AiChat, audience);
}
