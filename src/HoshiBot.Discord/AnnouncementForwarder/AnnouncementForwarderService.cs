using System.Security.Cryptography;
using System.Text;
using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.AnnouncementForwarder;

// Auto-translates official announcements (typically crossposted/webhook messages, e.g. Scopely's
// English STFC news) posted in configured source channels and reposts a branded translation into a
// destination channel — so members who don't read the source language still see them. Hooked into
// AiChatMessageHandler's single MESSAGE_CREATE path for live forwarding, AnnouncementForwarderCatchUpJob
// for anything missed while the bot was down, and AiChatIndexReconcileHandler's MESSAGE_UPDATE path so
// an edited source announcement updates its translation in place. Every forward is tracked in
// ForwardedAnnouncements (source message → destination message), which is what makes catch-up
// idempotent and lets an edit find the right message to update. See the forwarder plan.
public class AnnouncementForwarderService(
    HoshiBotDbContext db,
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

    // Called for a live MESSAGE_CREATE and by the catch-up job re-scanning recent source-channel
    // messages — the ForwardedAnnouncements lookup below makes re-scanning an already-forwarded
    // message a cheap no-op, which is what makes catch-up idempotent. Takes guildId explicitly
    // (mirrors AiChatIndexService.IndexMessageAsync) rather than reading message.GuildId: the
    // catch-up job's messages come from FetchRecentAsync as plain RestMessage, which doesn't
    // reliably carry a guild id, only the live gateway Message does.
    public async Task MaybeForwardAsync(ulong guildId, RestMessage message, CancellationToken cancellationToken)
    {
        if (message.Author.Id == gatewayClient.Id)
            return;

        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.AnnouncementForwarder, Audience, null))
            return;

        var sourceChannels = await channelService.GetEnabledAudienceChannelsAsync(guildId, GuildFeature.AnnouncementForwarder);
        if (!sourceChannels.Contains(message.ChannelId))
            return;

        var destinationChannel = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.AnnouncementForwarder, Audience, null, AnnouncementForwarderSettingKeys.Channel);
        if (destinationChannel is not { } destinationChannelId)
            return;

        if (await db.ForwardedAnnouncements.AnyAsync(f => f.SourceMessageId == message.Id, cancellationToken))
            return; // already forwarded (a re-scan by the catch-up job, or a duplicate event)

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

            var jumpLink = JumpLink(guildId, message.ChannelId, message.Id);
            var embed = await BuildEmbedAsync(guildId, translation, jumpLink, updated: false);

            // Never let translated pings re-notify the server — the source announcement already did.
            var posted = await gatewayClient.Rest.SendMessageAsync(destinationChannelId, new MessageProperties
            {
                Embeds = [embed],
                AllowedMentions = AllowedMentionsProperties.None,
            }, cancellationToken: cancellationToken);

            db.ForwardedAnnouncements.Add(new ForwardedAnnouncement
            {
                GuildId = guildId,
                SourceChannelId = message.ChannelId,
                SourceMessageId = message.Id,
                DestinationChannelId = destinationChannelId,
                DestinationMessageId = posted.Id,
                SourceContentHash = ComputeHash(sourceText),
                ForwardedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Announcement forwarding failed for guild {GuildId}, message {MessageId}", guildId, message.Id);
        }
    }

    // Called from the MESSAGE_UPDATE path when a previously-forwarded source announcement is edited —
    // re-translates and updates the existing destination message in place instead of posting a new one.
    // A no-op for a message that was never forwarded (nothing tracked yet) or whose text didn't
    // actually change (a cosmetic MESSAGE_UPDATE, e.g. Discord's own link-embed refresh).
    public async Task MaybeUpdateForwardAsync(ulong guildId, RestMessage message, CancellationToken cancellationToken)
    {
        if (message.Author.Id == gatewayClient.Id)
            return;

        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.AnnouncementForwarder, Audience, null))
            return;

        var tracked = await db.ForwardedAnnouncements.FirstOrDefaultAsync(f => f.SourceMessageId == message.Id, cancellationToken);
        if (tracked is null)
            return;

        var sourceText = AiChatIndexService.RenderMessageText(message);
        if (string.IsNullOrWhiteSpace(sourceText))
            return;

        var newHash = ComputeHash(sourceText);
        if (newHash == tracked.SourceContentHash)
            return; // cosmetic edit — the actual text didn't change

        try
        {
            var model = await modelResolver.ResolveLightweightAsync(guildId);
            if (model.Provider.Kind == AiProvider.Gemini && string.IsNullOrWhiteSpace(model.ApiKey))
                return;

            var targetLanguage = await ResolveTargetLanguageAsync(guildId);
            var translation = await translator.TranslateAsync(model, sourceText, targetLanguage, cancellationToken);
            if (translation is null)
                return; // leave the existing translation in place; a later edit can still retry

            var jumpLink = JumpLink(guildId, tracked.SourceChannelId, tracked.SourceMessageId);
            var embed = await BuildEmbedAsync(guildId, translation, jumpLink, updated: true);
            var now = DateTimeOffset.UtcNow;

            try
            {
                await gatewayClient.Rest.ModifyMessageAsync(tracked.DestinationChannelId, tracked.DestinationMessageId,
                    m => m.Embeds = [embed], cancellationToken: cancellationToken);
            }
            catch (RestException)
            {
                // The destination message is gone (a human deleted it) — self-heal by posting a fresh one.
                var posted = await gatewayClient.Rest.SendMessageAsync(tracked.DestinationChannelId, new MessageProperties
                {
                    Embeds = [embed],
                    AllowedMentions = AllowedMentionsProperties.None,
                }, cancellationToken: cancellationToken);
                tracked.DestinationMessageId = posted.Id;
            }

            tracked.SourceContentHash = newHash;
            tracked.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Announcement forward update failed for guild {GuildId}, message {MessageId}", guildId, message.Id);
        }
    }

    private async Task<EmbedProperties> BuildEmbedAsync(ulong guildId, string translation, string jumpLink, bool updated)
    {
        var fields = new List<EmbedFieldProperties> { new() { Name = "Original", Value = $"[Zur ursprünglichen Ankündigung]({jumpLink})" } };
        if (updated)
            fields.Add(new EmbedFieldProperties { Name = "Aktualisiert", Value = $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>" });

        return new EmbedProperties
        {
            Title = "🌐 Automatische Übersetzung",
            Description = translation,
            Color = EmbedBranding.InformationColor,
            Author = await embedBranding.BuildAuthorAsync(guildId),
            // No Timestamp: Discord renders it as a "• <time>" stamp appended right after the footer
            // text, which read as noise here — the "Aktualisiert" field already carries the when-edited
            // info when relevant.
            Footer = embedBranding.BuildFooter(guildId),
            Fields = fields,
        };
    }

    private static string JumpLink(ulong guildId, ulong channelId, ulong messageId) =>
        $"https://discord.com/channels/{guildId}/{channelId}/{messageId}";

    private static string ComputeHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private async Task<string> ResolveTargetLanguageAsync(ulong guildId)
    {
        var configured = await settingsService.GetTextAsync(guildId, GuildFeature.AnnouncementForwarder, Audience, null, AnnouncementForwarderSettingKeys.TargetLanguage);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        var locale = gatewayClient.Cache.Guilds.GetValueOrDefault(guildId)?.PreferredLocale;
        return FtsLanguage.FromDiscordLocale(locale);
    }
}
