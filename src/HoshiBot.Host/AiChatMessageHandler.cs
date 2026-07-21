using HoshiBot.Discord.AiChat;
using HoshiBot.Discord.AnnouncementForwarder;
using HoshiBot.Discord.MemberLore;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Rest;

namespace HoshiBot.Host;

// The single MESSAGE_CREATE handler. Guild messages go to AiChatService (decide whether/how to
// answer); direct messages (no guild) go to the member-lore interview if one is active. Kept thin:
// the gating/gathering/LLM logic lives in the scoped services — this owns only the Discord message
// lifecycle (post/edit). Auto-registered by AddGatewayHandlers(typeof(Program).Assembly).
//
// Directly-addressed answers stream: the service calls back with the answer-so-far, and we post a
// placeholder then edit it in place so a long generation appears live instead of a minute of
// "typing" then a wall of text. Passive answers don't stream (they may end in [NO_ANSWER] silence)
// — those come back as a single reply we post once.
//
// Requires the privileged Message Content intent (GatewayIntents.MessageContent, also enabled in
// the Discord Developer Portal) — without it message.Content arrives empty and the bot never
// finds anything to answer.
public class AiChatMessageHandler(IServiceScopeFactory scopeFactory, GatewayClient gatewayClient, ILogger<AiChatMessageHandler> logger)
    : IMessageCreateGatewayHandler
{
    public async ValueTask HandleAsync(Message message)
    {
        // Never react to or index our own messages (would be circular).
        if (message.Author.Id == gatewayClient.Id)
            return;

        // Direct messages (no guild) → the member-lore interview (real members only, not other bots).
        if (message.GuildId is null)
        {
            if (!message.Author.IsBot)
                await HandleDirectMessageAsync(message);
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();

            // Live-index the message if it's in a knowledge channel (keeps the search index fresh
            // between periodic backfills). Done for webhook/crossposted messages too: official
            // announcements (e.g. maintenance notices) are exactly the content Hoshi should answer
            // from, and were previously only picked up by the slow backfill — so a freshly-posted
            // announcement wasn't searchable for up to a backfill cycle. Independent of replying.
            var index = scope.ServiceProvider.GetRequiredService<AiChatIndexService>();
            await index.MaybeIndexIncomingAsync(message, CancellationToken.None);

            // Auto-translate + repost official announcements (also webhook/bot authors, e.g. Scopely's
            // crossposted news) into the configured destination channel. Independent of replying.
            var forwarder = scope.ServiceProvider.GetRequiredService<AnnouncementForwarderService>();
            await forwarder.MaybeForwardAsync(message.GuildId.Value, message, CancellationToken.None);

            // Only *answer* real members — never bots/webhooks (their content is still indexed above).
            if (message.Author.IsBot)
                return;

            var aiChat = scope.ServiceProvider.GetRequiredService<AiChatService>();

            // Streaming sink: post the placeholder on the first partial, then edit in place. No
            // mention pings on the streamed message — the reply already notifies the asker, and
            // editing shouldn't re-ping on every chunk.
            RestMessage? streamed = null;
            async ValueTask OnPartial(string partial)
            {
                if (streamed is null)
                    streamed = await message.ReplyAsync(new ReplyMessageProperties { Content = partial, AllowedMentions = AllowedMentionsProperties.None });
                else
                    await gatewayClient.Rest.ModifyMessageAsync(streamed.ChannelId, streamed.Id,
                        m => { m.Content = partial; m.AllowedMentions = AllowedMentionsProperties.None; });
            }

            var reply = await aiChat.TryBuildReplyAsync(message, OnPartial, CancellationToken.None);

            if (streamed is not null)
            {
                // Already streamed: one last edit with the authoritative finalized text. (An addressed
                // message always yields a reply, so the null case is only defensive — leave the last
                // partial in place.)
                if (reply is { } finalReply)
                    await gatewayClient.Rest.ModifyMessageAsync(streamed.ChannelId, streamed.Id,
                        m => { m.Content = finalReply.Text; m.AllowedMentions = AllowedMentionsProperties.None; });
            }
            else if (reply is { } r)
            {
                // Not streamed (passive): post once. Never let the AI's output ping @everyone, roles,
                // or the replied-to user; only the specific conversation participants it was told about
                // may be pinged (so a stray/hallucinated <@id> can't ping a random member).
                await message.ReplyAsync(new ReplyMessageProperties
                {
                    Content = r.Text,
                    AllowedMentions = new AllowedMentionsProperties
                    {
                        Everyone = false,
                        ReplyMention = false,
                        AllowedRoles = [],
                        AllowedUsers = r.AllowedUserIds,
                    },
                });
            }
        }
        catch (RestException ex)
        {
            logger.LogWarning(ex, "AiChat failed to post a reply in channel {ChannelId}", message.ChannelId);
        }
    }

    private async ValueTask HandleDirectMessageAsync(Message message)
    {
        var content = message.Content?.Trim();
        if (string.IsNullOrEmpty(content))
            return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var interviews = scope.ServiceProvider.GetRequiredService<MemberInterviewService>();
            await interviews.HandleReplyAsync(message.Author.Id, content, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Member-interview DM handling failed for user {UserId}", message.Author.Id);
        }
    }
}
