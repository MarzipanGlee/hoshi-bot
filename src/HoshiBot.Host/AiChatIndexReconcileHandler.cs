using HoshiBot.Discord.AiChat;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace HoshiBot.Host;

// Keeps the AI-chat knowledge index in step with Discord edits and deletions that the periodic
// backfill can't cover on its own: the backfill only re-indexes each channel's *recent* page, so an
// edit to an older message, or any deletion, would otherwise leave stale content (and a stale
// embedding) in the index indefinitely.
//
// - MESSAGE_UPDATE → re-run the same knowledge-channel index upsert as a new message; the upsert
//   drops the stored embedding when the text actually changed so it gets re-embedded.
// - MESSAGE_DELETE / MESSAGE_DELETE_BULK → remove the row(s).
//
// Uses GuildMessages intent (already enabled). MaybeIndexIncomingAsync ignores bot authors, so the
// bot's own streamed message edits don't get indexed here.
public class AiChatIndexReconcileHandler(IServiceScopeFactory scopeFactory, ILogger<AiChatIndexReconcileHandler> logger)
    : IMessageUpdateGatewayHandler, IMessageDeleteGatewayHandler, IMessageDeleteBulkGatewayHandler
{
    public async ValueTask HandleAsync(Message message)
    {
        if (message.GuildId is null || message.Author.IsBot)
            return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var index = scope.ServiceProvider.GetRequiredService<AiChatIndexService>();
            await index.MaybeIndexIncomingAsync(message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AiChat index reconcile (edit) failed for message {MessageId}", message.Id);
        }
    }

    public ValueTask HandleAsync(MessageDeleteEventArgs args) =>
        RemoveAsync([args.MessageId], args.MessageId);

    public ValueTask HandleAsync(MessageDeleteBulkEventArgs args) =>
        RemoveAsync(args.MessageIds, args.MessageIds.Count == 0 ? 0 : args.MessageIds[0]);

    private async ValueTask RemoveAsync(IReadOnlyCollection<ulong> messageIds, ulong sampleId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var index = scope.ServiceProvider.GetRequiredService<AiChatIndexService>();
            await index.RemoveIndexedMessagesAsync(messageIds, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AiChat index reconcile (delete) failed for message {MessageId}", sampleId);
        }
    }
}
