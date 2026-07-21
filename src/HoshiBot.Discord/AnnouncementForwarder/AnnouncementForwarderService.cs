using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.AnnouncementForwarder;

// Auto-translates official announcements (typically crossposted/webhook messages, e.g. Scopely's
// English STFC news) posted in configured source channels and reposts a branded translation into a
// destination channel — so members who don't read the source language still see them. Hooked into
// AiChatMessageHandler's single MESSAGE_CREATE path (create-time only for v1: an announcement posted
// while the bot is down isn't caught, but the English original is still there). See the forwarder plan.
public class AnnouncementForwarderService(
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureChannelService channelService,
    GuildFeatureSettingsService settingsService,
    AiChatModelResolver modelResolver,
    AnnouncementTranslator translator,
    EmbedBranding embedBranding,
    ILogger<AnnouncementForwarderService> logger)
{
    private const GuildAudience Audience = GuildAudience.Guild;

    public async Task MaybeForwardAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.GuildId is not { } guildId || message.Author.Id == gatewayClient.Id)
            return;

        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.AnnouncementForwarder, Audience, null))
            return;

        var sourceChannels = await channelService.GetEnabledAudienceChannelsAsync(guildId, GuildFeature.AnnouncementForwarder);
        if (!sourceChannels.Contains(message.ChannelId))
            return;

        var destinationChannel = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.AnnouncementForwarder, Audience, null, AnnouncementForwarderSettingKeys.Channel);
        if (destinationChannel is not { } destinationChannelId)
            return;

        var sourceText = AiChatIndexService.RenderMessageText(message);
        if (string.IsNullOrWhiteSpace(sourceText))
            return;

        try
        {
            var model = await modelResolver.ResolveLightweightAsync(guildId);
            if (model.Provider.Kind == AiProvider.Gemini && string.IsNullOrWhiteSpace(model.ApiKey))
                return;

            var targetLanguage = await ResolveTargetLanguageAsync(guildId);
            var translation = await translator.TranslateAsync(model, sourceText, targetLanguage, cancellationToken);
            if (translation is null)
                return; // already in the target language, or the model failed — nothing to forward

            var jumpLink = $"https://discord.com/channels/{guildId}/{message.ChannelId}/{message.Id}";
            var embed = new EmbedProperties
            {
                Title = "🌐 Automatische Übersetzung",
                Description = translation,
                Color = EmbedBranding.InformationColor,
                Author = await embedBranding.BuildAuthorAsync(guildId),
                Footer = embedBranding.BuildFooter(guildId),
                Timestamp = DateTimeOffset.UtcNow,
                Fields = [new EmbedFieldProperties { Name = "Original", Value = $"[Zur ursprünglichen Ankündigung]({jumpLink})" }],
            };

            // Never let translated pings re-notify the server — the source announcement already did.
            await gatewayClient.Rest.SendMessageAsync(destinationChannelId, new MessageProperties
            {
                Embeds = [embed],
                AllowedMentions = AllowedMentionsProperties.None,
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Announcement forwarding failed for guild {GuildId}, message {MessageId}", guildId, message.Id);
        }
    }

    private async Task<string> ResolveTargetLanguageAsync(ulong guildId)
    {
        var configured = await settingsService.GetTextAsync(guildId, GuildFeature.AnnouncementForwarder, Audience, null, AnnouncementForwarderSettingKeys.TargetLanguage);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        var locale = gatewayClient.Cache.Guilds.GetValueOrDefault(guildId)?.PreferredLocale;
        return FtsLanguage.FromDiscordLocale(locale);
    }
}
