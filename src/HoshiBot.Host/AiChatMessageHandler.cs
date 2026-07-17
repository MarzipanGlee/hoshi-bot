using HoshiBot.Discord.AiChat;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Rest;

namespace HoshiBot.Host;

// Listens to every guild message (MESSAGE_CREATE) and lets AiChatService decide whether to
// answer. Kept deliberately thin: all the gating/gathering/LLM logic lives in the scoped
// AiChatService — this singleton handler just resolves a scope per message and posts the reply
// when one comes back. Auto-registered by AddGatewayHandlers(typeof(Program).Assembly), same as
// GuildSyncHandler.
//
// Requires the privileged Message Content intent (GatewayIntents.MessageContent, also enabled in
// the Discord Developer Portal) — without it message.Content arrives empty and the bot never
// finds anything to answer.
public class AiChatMessageHandler(IServiceScopeFactory scopeFactory, ILogger<AiChatMessageHandler> logger)
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
            if (await aiChat.TryBuildReplyAsync(message, CancellationToken.None) is not { } reply)
                return;

            await message.ReplyAsync(new ReplyMessageProperties
            {
                Content = reply.Text,
                // Never let the AI's output ping @everyone, roles, or the replied-to user; only the
                // specific conversation participants it was told about may actually be pinged (so a
                // stray/hallucinated <@id> can't ping a random member).
                AllowedMentions = new AllowedMentionsProperties
                {
                    Everyone = false,
                    ReplyMention = false,
                    AllowedRoles = [],
                    AllowedUsers = reply.AllowedUserIds,
                },
            });
        }
        catch (RestException ex)
        {
            logger.LogWarning(ex, "AiChat failed to post a reply in channel {ChannelId}", message.ChannelId);
        }
    }
}
