using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.AiChat;

public class AiChatFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.AiChat;
    public string Slug => "ai-chat";
    public string Title => "AI Chat";

    public string Description =>
        "Lets the bot answer questions conversationally (via Google Gemini) in a configurable set of " +
        "listen channels, grounded in a configurable set of knowledge channels. It can also build a " +
        "genuine memory over time — notable community events, past conversations, and its own history " +
        "with individual members — so answers feel like they come from someone who was actually there. " +
        "Each guild uses its own Gemini API key.";

    public string Icon => "oi-chat";
    public Type EditorComponentType => typeof(AiChatEditor);

    public IReadOnlyList<FeatureExtraPage> ExtraPages =>
        [new FeatureExtraPage("memories", "Memories", typeof(MemoryAdmin))];

    // The AI provider/key/model now live in the guild-wide AiBackend feature (declared as a
    // dependency, so its "not configured" state surfaces via the dependency badge). AiChat's own
    // "configured" signal is therefore about having at least one listen channel to answer in for
    // this audience — enabled but with no listen channel does nothing.
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.GuildFeatureChannels.AnyAsync(
            c => c.GuildId == guildId && c.Feature == GuildFeature.AiChat && c.Audience == audience);
    }
}
