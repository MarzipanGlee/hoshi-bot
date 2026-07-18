using HoshiBot.Discord.AiChat;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Rest;

namespace HoshiBot.Host;

// Listens to every guild message (MESSAGE_CREATE) and lets AiChatService decide whether to
// answer. Kept deliberately thin: all the gating/gathering/LLM logic lives in the scoped
// AiChatService — this singleton handler owns only the Discord message lifecycle (post/edit).
// Auto-registered by AddGatewayHandlers(typeof(Program).Assembly), same as GuildSyncHandler.
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
        // Cheap pre-filters before spinning up a DI scope for the common no-op case.
        if (message.GuildId is null || message.Author.IsBot)
            return;

        try
        {
            using var scope = scopeFactory.CreateScope();

            // Live-index the message if it's in a knowledge channel (keeps the search index fresh
            // between periodic backfills). Independent of whether we reply.
            var index = scope.ServiceProvider.GetRequiredService<AiChatIndexService>();
            await index.MaybeIndexIncomingAsync(message, CancellationToken.None);

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
}
