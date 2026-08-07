using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.Announcements;

// How an announcement gets started: staff write a draft in the prep channel, the bot decorates it
// with 🟩 🟨 🟥 🟦, and clicking one opens the publish confirm for that severity. Restores the legacy
// bot's flow (hoshi-bot-yagpdb/Commands/announcements/) exactly — it replaced a "Create preview"
// context-menu command, which needed staff to know a right-click menu existed and then pick the
// severity from a second prompt.
//
// Both halves are reaction-driven, so both need GatewayIntents.GuildMessageReactions (Host
// Program.cs) and the Add Reactions permission in the draft channel (declared as
// ChannelAccessProfile.Draft). Missing the permission does not throw — a draft channel nobody can
// react in is a configuration problem, not a reason to tear down message handling — but it is
// reported to the guild's admin channel, because it is otherwise completely silent: nothing about
// the draft looks wrong, staff just post into a channel where nothing ever happens.
public class AnnouncementDraftService(
    GatewayClient gatewayClient,
    AnnouncementService announcementService,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    LanguageResolver languageResolver,
    NotificationDispatcher dispatcher,
    ILogger<AnnouncementDraftService> logger)
{
    // Called from AiChatMessageHandler's single MESSAGE_CREATE path. Every non-bot message in a
    // configured draft channel gets the four reactions — as in legacy, there is no "is this a real
    // draft" heuristic: staff simply don't click on a message that wasn't meant as one.
    public async Task MaybeAddDraftReactionsAsync(ulong guildId, Message message, CancellationToken cancellationToken)
    {
        if (message.Author.Id == gatewayClient.Id || message.Author.IsBot)
            return;

        if (!await IsDraftChannelAsync(guildId, message.ChannelId))
            return;

        await AddDraftReactionsAsync(guildId, message.ChannelId, message.Id, cancellationToken);
    }

    // Split out so the cancel button can put the squares back on a draft whose preview was
    // discarded — without them the draft is stranded and has to be reposted to be publishable.
    public async Task AddDraftReactionsAsync(ulong guildId, ulong channelId, ulong messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Sequential on purpose: Discord orders reactions by when they were added, so adding
            // them in parallel would shuffle 🟩🟨🟥🟦 into an arbitrary order on the message.
            foreach (var severity in AnnouncementSeverities.Ordered)
                await gatewayClient.Rest.AddMessageReactionAsync(channelId, messageId, AnnouncementSeverities.Emoji(severity), cancellationToken: cancellationToken);
        }
        catch (RestException ex)
        {
            logger.LogWarning(ex, "Could not add announcement draft reactions in channel {ChannelId}", channelId);

            // Worth telling an admin about rather than only logging: without the reactions there is
            // no way to publish an announcement at all, and nothing else about the draft looks wrong
            // — staff just post into a channel where nothing ever happens.
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, BotAction.AddDraftReactions, channelId,
                BotPermission.ViewChannel | BotPermission.AddReactions);
        }
    }

    // Called from the MESSAGE_REACTION_ADD handler for every reaction in the guild — the emoji and
    // channel checks below are what narrow that down to a draft being published.
    public async Task HandleDraftReactionAsync(ulong guildId, ulong channelId, ulong messageId, ulong userId, string? emoji, CancellationToken cancellationToken)
    {
        // Our own four reactions arrive through this same event — reacting to them would open a
        // publish prompt the moment a draft is posted.
        if (userId == gatewayClient.Id)
            return;

        if (AnnouncementSeverities.FromEmoji(emoji) is not { } severity)
            return;

        var scopes = await settingsService.FindScopesByValueAsync(guildId, GuildFeature.Announcements, AnnouncementsSettingKeys.DraftChannel, channelId);
        if (scopes.Count == 0 || !await featureService.IsEnabledAsync(guildId, GuildFeature.Announcements))
            return;

        try
        {
            var draft = await gatewayClient.Rest.GetMessageAsync(channelId, messageId, cancellationToken: cancellationToken);

            // The publish prompts this posts live in the draft channel too, and carry no reactions
            // of their own — but nothing stops someone reacting 🟨 to one by hand.
            if (draft.Author.Id == gatewayClient.Id)
                return;

            await TryRemoveReactionAsync(channelId, messageId, severity, userId, cancellationToken);

            // The prompt goes to the reacting staff member — their language, not the target
            // scope's (the published post itself is rendered in the scope language downstream).
            var lang = await languageResolver.ForUserAsync(userId, scopeGuildId: guildId);

            // Unambiguous once an admin has split each audience's draft channel apart (the common
            // case, including every guild that never splits — it only ever has one draft channel
            // to begin with). Right after a guild's migration, before any splitting, a channel can
            // still match 2+ audiences (all pointing at the same legacy channel) — ask explicitly
            // rather than guessing. The specific alliance is resolved at publish time as the
            // primary link, so audience is all that's picked here.
            var audiences = scopes.Select(s => s.Audience).Distinct().ToList();

            // The preview embed is the real published one, so it renders in the guild language
            // rather than the clicking staff member's — the alliance isn't resolved until publish,
            // and showing an English "Severity"/"On behalf of" over a German announcement would be
            // a preview of something that will never exist.
            var scopeLang = await languageResolver.ForGuildAsync(guildId);
            var (preview, _) = await announcementService.BuildAnnouncementEmbedAsync(guildId, draft, severity, scopeLang);

            // draft.Author is a plain User — a message's author carries no guild nickname — so the
            // salutation would fall back to the global name. Fetch the member to address them the
            // way the rest of the bot does, by their tag-stripped nickname.
            var commander = CommanderName.Of(await ResolveMemberAsync(guildId, draft.Author, cancellationToken));

            MessageProperties prompt;
            if (audiences.Count == 1)
            {
                var targets = await announcementService.PublishTargetNamesAsync(guildId, audiences[0], scopes[0].GuildAllianceId);
                prompt = AnnouncementButtonModule.BuildPublishPrompt(draft, preview, commander, audiences[0], severity, targets, lang);
            }
            else
            {
                prompt = AnnouncementButtonModule.BuildAudiencePrompt(draft, preview, commander, severity, lang);
            }

            await gatewayClient.Rest.SendMessageAsync(channelId, prompt, cancellationToken: cancellationToken);

            // Only after the preview is actually posted: pulling them first would strand the draft
            // if the send failed.
            await RemoveDraftReactionsAsync(channelId, messageId, cancellationToken);
        }
        catch (RestException ex)
        {
            logger.LogWarning(ex, "Announcement draft reaction handling failed for message {MessageId} in channel {ChannelId}", messageId, channelId);
        }
    }

    // Legacy cleared the clicked reaction, which acknowledges the click and makes the same emoji
    // clickable again (Discord ignores a repeated identical reaction from the same user). Deleting
    // *another user's* reaction needs Manage Messages, which ChannelAccessProfile.Draft now declares
    // — the same permission the post-publish draft deletion needs, scoped to this channel alone.
    // Still caught rather than propagated: an ungranted draft channel is a configuration problem the
    // permission audit reports, not a reason to abandon a publish that is otherwise fine.
    private async Task TryRemoveReactionAsync(ulong channelId, ulong messageId, AnnouncementSeverity severity, ulong userId, CancellationToken cancellationToken)
    {
        try
        {
            await gatewayClient.Rest.DeleteUserMessageReactionAsync(channelId, messageId, AnnouncementSeverities.Emoji(severity), userId, cancellationToken: cancellationToken);
        }
        catch (RestException ex)
        {
            logger.LogDebug(ex, "Could not clear the {Severity} reaction in channel {ChannelId} (no Manage Messages?)", severity, channelId);
        }
    }

    // Takes the bot's own four squares off a draft that now has a preview open, so a second
    // staff member can't start a competing publish for the same draft — and so the draft stops
    // looking like it still needs a decision. Cancelling puts them back.
    //
    // Unlike clearing another member's reaction this needs NO permission: Discord's
    // "delete own reaction" endpoint is always allowed, which is why this one isn't best-effort
    // in the same apologetic way TryRemoveReactionAsync is.
    public async Task RemoveDraftReactionsAsync(ulong channelId, ulong messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var severity in AnnouncementSeverities.Ordered)
                await gatewayClient.Rest.DeleteCurrentUserMessageReactionAsync(channelId, messageId, AnnouncementSeverities.Emoji(severity), cancellationToken: cancellationToken);
        }
        catch (RestException ex)
        {
            logger.LogWarning(ex, "Could not clear the bot's draft reactions in channel {ChannelId}", channelId);
        }
    }

    // Falls back to the plain User when the member can't be fetched (they left, or the call fails):
    // a slightly less personal salutation beats failing the whole publish flow over a name.
    private async Task<User> ResolveMemberAsync(ulong guildId, User author, CancellationToken cancellationToken)
    {
        try
        {
            return await gatewayClient.Rest.GetGuildUserAsync(guildId, author.Id, cancellationToken: cancellationToken);
        }
        catch (RestException ex)
        {
            logger.LogDebug(ex, "Could not resolve the draft author {UserId} in guild {GuildId} for the salutation", author.Id, guildId);
            return author;
        }
    }

    private async Task<bool> IsDraftChannelAsync(ulong guildId, ulong channelId) =>
        await featureService.IsEnabledAsync(guildId, GuildFeature.Announcements)
        && (await settingsService.FindScopesByValueAsync(guildId, GuildFeature.Announcements, AnnouncementsSettingKeys.DraftChannel, channelId)).Count > 0;
}
