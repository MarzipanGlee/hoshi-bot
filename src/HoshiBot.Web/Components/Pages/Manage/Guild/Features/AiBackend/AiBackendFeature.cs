using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.AiBackend;

// The guild-wide AI backend configuration: provider, API key, and model choices shared by every
// AI-powered feature (AiChat, MemberLore, AnnouncementForwarder all declare it as a dependency).
// Enum/folder are named "AiBackend" because the AiProvider name is taken by the Gemini/Ollama
// backend enum; the user-facing Title is the friendlier "AI Provider".
public class AiBackendFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.AiBackend;
    public string Slug => "ai-backend";

    public string Icon => "oi-cog";
    public Type EditorComponentType => typeof(AiBackendEditor);

    // "Configured" means the backend can actually answer: a local Ollama provider needs no key, so
    // it's configured as soon as it's selected; Gemini needs this guild's API key. Guild-wide scope.
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        var provider = await context.GetTextAsync(guildId, GuildFeature.AiBackend, GuildAudience.Guild, null, AiBackendSettingKeys.Provider);
        if (string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
            return true;

        var apiKey = await context.GetTextAsync(guildId, GuildFeature.AiBackend, GuildAudience.Guild, null, AiBackendSettingKeys.ApiKey);
        return !string.IsNullOrWhiteSpace(apiKey);
    }
}
