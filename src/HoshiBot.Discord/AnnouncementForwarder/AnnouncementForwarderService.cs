using System.Security.Cryptography;
using System.Text;
using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Discord.ReadReceipts;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
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
    LanguageResolver languageResolver,
    ReadReceiptService readReceipts,
    ILogger<AnnouncementForwarderService> logger)
{
    // One forward per (audience, alliance) that watches the source channel, each to its own
    // destination. The Alliance audience fans out across the guild's enabled alliances, since a
    // GuildFeatureChannel carries no alliance id — the channel is shared, the destinations are not.
    private readonly record struct ForwardTarget(GuildAudience Audience, int? GuildAllianceId, ulong DestinationChannelId);

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

        var targets = await ResolveTargetsAsync(guildId, message.ChannelId);
        if (targets.Count == 0)
            return;

        var sourceText = AiChatIndexService.RenderMessageText(message);
        if (string.IsNullOrWhiteSpace(sourceText))
            return;

        try
        {
            var model = await modelResolver.ResolveLightweightAsync(guildId);
            if (model.Provider.Kind == AiProvider.Gemini && string.IsNullOrWhiteSpace(model.ApiKey))
                return;

            // Translations are cached per target language across targets: two alliances configured
            // for the same language share one model call rather than paying for the same work twice.
            var translations = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var target in targets)
            {
                // Per destination, not per source message — the same announcement legitimately
                // reaches several channels now, and only THIS one may already have been done.
                if (await db.ForwardedAnnouncements.AnyAsync(
                        f => f.SourceMessageId == message.Id && f.DestinationChannelId == target.DestinationChannelId, cancellationToken))
                    continue;

                var targetLanguage = await ResolveTargetLanguageAsync(guildId, target.Audience, target.GuildAllianceId);
                if (!translations.TryGetValue(targetLanguage, out var translation))
                    translations[targetLanguage] = translation = await translator.TranslateAsync(model, sourceText, targetLanguage, cancellationToken);

                if (translation is null)
                    continue; // already in the target language, or the model failed — nothing to forward

                var jumpLink = JumpLink(guildId, message.ChannelId, message.Id);
                var embed = await BuildEmbedAsync(guildId, translation, jumpLink, updated: false);

                // Never let translated pings re-notify the server — the source announcement already did.
                var posted = await gatewayClient.Rest.SendMessageAsync(target.DestinationChannelId, new MessageProperties
                {
                    Embeds = [embed],
                    AllowedMentions = AllowedMentionsProperties.None,
                }, cancellationToken: cancellationToken);

                db.ForwardedAnnouncements.Add(new ForwardedAnnouncement
                {
                    GuildId = guildId,
                    SourceChannelId = message.ChannelId,
                    SourceMessageId = message.Id,
                    DestinationChannelId = target.DestinationChannelId,
                    DestinationMessageId = posted.Id,
                    SourceContentHash = ComputeHash(sourceText),
                    ForwardedAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync(cancellationToken);

                // Forwarded posts are by far the commonest thing worth confirming — 97 of them
                // against 3 announcements when read tracking was made kind-aware — so they register
                // like any other readable post and pick up the button when the kind is switched on.
                await RegisterReadablePostAsync(guildId, target, posted.Id, translation, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Announcement forwarding failed for guild {GuildId}, message {MessageId}", guildId, message.Id);
        }
    }

    // Every enabled scope watching this source channel, paired with the destination it forwards to.
    // A scope with no destination configured is skipped rather than failing the others.
    private async Task<List<ForwardTarget>> ResolveTargetsAsync(ulong guildId, ulong sourceChannelId)
    {
        var audiences = await channelService.GetAudiencesForChannelAsync(guildId, GuildFeature.AnnouncementForwarder, sourceChannelId);
        if (audiences.Count == 0)
            return [];

        var targets = new List<ForwardTarget>();
        foreach (var audience in audiences)
        {
            // GuildFeatureChannel has no alliance id, so an Alliance-audience source channel is
            // shared by every alliance that has the feature on — each with its own destination.
            var allianceIds = audience == GuildAudience.Alliance
                ? (await featureService.GetEnabledAllianceIdsAsync(guildId, GuildFeature.AnnouncementForwarder)).Select(id => (int?)id).ToList()
                : [null];

            foreach (var allianceId in allianceIds)
            {
                if (await settingsService.GetSnowflakeAsync(guildId, GuildFeature.AnnouncementForwarder, audience, allianceId, AnnouncementForwarderSettingKeys.Channel) is { } destination)
                    targets.Add(new ForwardTarget(audience, allianceId, destination));
            }
        }

        return targets;
    }

    // Called from the MESSAGE_UPDATE path when a previously-forwarded source announcement is edited —
    // re-translates and updates the existing destination message in place instead of posting a new one.
    // A no-op for a message that was never forwarded (nothing tracked yet) or whose text didn't
    // actually change (a cosmetic MESSAGE_UPDATE, e.g. Discord's own link-embed refresh).
    public async Task MaybeUpdateForwardAsync(ulong guildId, RestMessage message, CancellationToken cancellationToken)
    {
        if (message.Author.Id == gatewayClient.Id)
            return;

        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.AnnouncementForwarder))
            return;

        // EVERY destination this source reached, not just the first: an edit that only refreshed one
        // alliance's copy would leave the others stating the old text with no sign of it.
        var tracked = await db.ForwardedAnnouncements
            .Where(f => f.SourceMessageId == message.Id)
            .ToListAsync(cancellationToken);
        if (tracked.Count == 0)
            return;

        var sourceText = AiChatIndexService.RenderMessageText(message);
        if (string.IsNullOrWhiteSpace(sourceText))
            return;

        var newHash = ComputeHash(sourceText);
        if (tracked.All(f => f.SourceContentHash == newHash))
            return; // cosmetic edit — the actual text didn't change

        try
        {
            var model = await modelResolver.ResolveLightweightAsync(guildId);
            if (model.Provider.Kind == AiProvider.Gemini && string.IsNullOrWhiteSpace(model.ApiKey))
                return;

            var translations = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var now = DateTimeOffset.UtcNow;

            foreach (var row in tracked.Where(f => f.SourceContentHash != newHash))
            {
                // The scope is recovered from the destination channel, the same way
                // AnnouncementService recovers an announcement's — the tracking row records where it
                // went, and that channel belongs to exactly one scope's setting.
                var (audience, guildAllianceId) = await ResolveScopeAsync(guildId, row.DestinationChannelId);
                var targetLanguage = await ResolveTargetLanguageAsync(guildId, audience, guildAllianceId);

                if (!translations.TryGetValue(targetLanguage, out var translation))
                    translations[targetLanguage] = translation = await translator.TranslateAsync(model, sourceText, targetLanguage, cancellationToken);

                if (translation is null)
                    continue; // leave the existing translation in place; a later edit can still retry

                var jumpLink = JumpLink(guildId, row.SourceChannelId, row.SourceMessageId);
                var embed = await BuildEmbedAsync(guildId, translation, jumpLink, updated: true);

                try
                {
                    await gatewayClient.Rest.ModifyMessageAsync(row.DestinationChannelId, row.DestinationMessageId,
                        m => m.Embeds = [embed], cancellationToken: cancellationToken);
                }
                catch (RestException)
                {
                    // The destination message is gone (a human deleted it) — self-heal by posting a fresh one.
                    var posted = await gatewayClient.Rest.SendMessageAsync(row.DestinationChannelId, new MessageProperties
                    {
                        Embeds = [embed],
                        AllowedMentions = AllowedMentionsProperties.None,
                    }, cancellationToken: cancellationToken);

                    // The old message took its read button with it. Move the tracking row to the
                    // replacement, or the ✅ points at a message that no longer exists and the count
                    // stops updating with no sign of why.
                    await readReceipts.MoveAsync(guildId, row.DestinationMessageId, posted.Id, cancellationToken);
                    row.DestinationMessageId = posted.Id;
                }

                row.SourceContentHash = newHash;
                row.UpdatedAt = now;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Announcement forward update failed for guild {GuildId}, message {MessageId}", guildId, message.Id);
        }
    }

    // Which scope owns a destination channel. Null audience/alliance where the channel is no longer
    // any scope's destination (an admin repointed it) — the caller then falls back to the guild.
    private async Task<(GuildAudience Audience, int? GuildAllianceId)> ResolveScopeAsync(ulong guildId, ulong destinationChannelId)
    {
        var scopes = await settingsService.FindScopesByValueAsync(guildId, GuildFeature.AnnouncementForwarder, AnnouncementForwarderSettingKeys.Channel, destinationChannelId);
        return scopes.Count > 0 ? scopes[0] : (GuildAudience.Guild, null);
    }

    // The unread list names posts, and every forward shares the same "Automatic translation" heading
    // — so the translated text's own first line is the title, exactly as an announcement's is.
    private async Task RegisterReadablePostAsync(ulong guildId, ForwardTarget target, ulong messageId, string translation, CancellationToken cancellationToken)
    {
        var channelId = target.DestinationChannelId;
        var lang = target.GuildAllianceId is { } allianceId
            ? await languageResolver.ForAllianceAsync(allianceId)
            : await languageResolver.ForGuildAsync(guildId);
        var firstLine = translation.Split('\n', 2)[0].Trim();
        var title = string.IsNullOrWhiteSpace(firstLine) ? Msg.Announce.ForwardTitle(lang) : firstLine;

        var post = await readReceipts.RegisterAsync(guildId, channelId, messageId, ReadablePostKind.ForwardedAnnouncement, target.Audience, target.GuildAllianceId, title, lang, cancellationToken: cancellationToken);
        if (!post.ReadReceiptsEnabled)
            return;

        await gatewayClient.Rest.ModifyMessageAsync(channelId, messageId,
            m => m.Components = [ReadReceiptService.Buttons(post, 0)], cancellationToken: cancellationToken);
    }

    private async Task<EmbedProperties> BuildEmbedAsync(ulong guildId, string translation, string jumpLink, bool updated)
    {
        // The forward's fixed labels render in the destination channel's owning scope's
        // language: the channel is a guild-level setting (Audience = Guild, no alliance id),
        // so that scope is the guild. Independent of ResolveTargetLanguageAsync — the
        // translation's own language is a free-text FTS setting, not necessarily a UI language.
        var lang = await languageResolver.ForGuildAsync(guildId);

        var fields = new List<EmbedFieldProperties> { new() { Name = Msg.Announce.ForwardFieldOriginal(lang), Value = Msg.Announce.ForwardOriginalLink(lang, jumpLink) } };
        if (updated)
            fields.Add(new EmbedFieldProperties { Name = Msg.Announce.ForwardFieldUpdated(lang), Value = $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>" });

        // No Timestamp: Discord renders it as a "• <time>" stamp appended right after the footer
        // text, which read as noise here — the "Aktualisiert" field already carries the when-edited
        // info when relevant.
        var embed = await embedBranding.BuildBrandedAsync(guildId, translation, EmbedBranding.InformationColor, Msg.Announce.ForwardTitle(lang));
        embed.Fields = fields;
        return embed;
    }

    private static string JumpLink(ulong guildId, ulong channelId, ulong messageId) =>
        $"https://discord.com/channels/{guildId}/{channelId}/{messageId}";

    private static string ComputeHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private async Task<string> ResolveTargetLanguageAsync(ulong guildId, GuildAudience audience, int? guildAllianceId)
    {
        var configured = await settingsService.GetTextAsync(guildId, GuildFeature.AnnouncementForwarder, audience, guildAllianceId, AnnouncementForwarderSettingKeys.TargetLanguage);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        var locale = gatewayClient.Cache.Guilds.GetValueOrDefault(guildId)?.PreferredLocale;
        return FtsLanguage.FromDiscordLocale(locale);
    }
}
