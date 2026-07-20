using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.AiChat;

public class AiChatFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.AiChat;
    public string Slug => "ai-chat";
    public string Title => "AI Chat";

    public string Description =>
        "Lets the bot answer questions conversationally (via Google Gemini) in a configurable set of " +
        "listen channels, grounded in a configurable set of knowledge channels. Each guild uses its own " +
        "Gemini API key.";

    public string Icon => "oi-chat";
    public Type EditorComponentType => typeof(AiChatEditor);

    // "Configured" means the guild has actually supplied its Gemini API key — without it the
    // feature stays silent even when enabled. The key is guild-wide (None/null scope), so it's the
    // same check on every audience tab.
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        var apiKey = await context.Settings.GetTextAsync(guildId, GuildFeature.AiChat, GuildAudience.None, null, AiChatSettingKeys.ApiKey);
        return !string.IsNullOrWhiteSpace(apiKey);
    }
}
