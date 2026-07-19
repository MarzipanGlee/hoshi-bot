using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Discord.AiChat;

// The guild's resolved AI backend: which provider, which model, and (Gemini only) the decrypted API
// key. Shared so features beyond the chat listener (e.g. the member-lore DM interview) can run a
// completion with the same per-guild configuration. Mirrors the resolution AiChatService does inline.
public sealed record ResolvedAiChatModel(IAiChatProvider Provider, string Model, string? ApiKey);

public class AiChatModelResolver(IEnumerable<IAiChatProvider> providers, GuildFeatureSettingsService settingsService)
{
    // The scalar AI-chat settings are guild-wide (one account per guild), stored at the None scope.
    private const GuildAudience SettingsScope = GuildAudience.None;

    public async Task<ResolvedAiChatModel> ResolveAsync(ulong guildId)
    {
        var provider = await ResolveProviderAsync(guildId);
        var apiKey = await settingsService.GetSecretAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.ApiKey);

        var model = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.Model);
        model = string.IsNullOrWhiteSpace(model) ? provider.DefaultModel : model.Trim();

        return new ResolvedAiChatModel(provider, model, apiKey);
    }

    // The model for member-lore background tasks (DM interviews + note extraction): same provider/key
    // as the main chat, but a lighter, cheaper model by default (the gate/flash-lite tier) so these
    // frequent calls don't spend the premium answer model's tiny per-day request quota. Overridable
    // per guild via AiChatSettingKeys.MemberLoreModel; falls back to the main model for a provider
    // with no gate model (e.g. local Ollama, where there's no quota to conserve).
    public async Task<ResolvedAiChatModel> ResolveLightweightAsync(ulong guildId)
    {
        var provider = await ResolveProviderAsync(guildId);
        var apiKey = await settingsService.GetSecretAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.ApiKey);

        var model = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.MemberLoreModel);
        model = string.IsNullOrWhiteSpace(model) ? (provider.DefaultGateModel ?? provider.DefaultModel) : model.Trim();

        return new ResolvedAiChatModel(provider, model, apiKey);
    }

    // The guild's configured chat backend (default Gemini on unset/unknown).
    public async Task<IAiChatProvider> ResolveProviderAsync(ulong guildId)
    {
        var configured = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.Provider);
        var kind = Enum.TryParse<AiProvider>(configured, ignoreCase: true, out var parsed) ? parsed : AiProvider.Gemini;
        return providers.First(p => p.Kind == kind);
    }
}
