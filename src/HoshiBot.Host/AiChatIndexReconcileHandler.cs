using HoshiBot.Discord.AiChat;
using HoshiBot.Discord.AnnouncementForwarder;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace HoshiBot.Host;

// Keeps the AI-chat knowledge index in step with Discord edits and deletions that the periodic
// backfill can't cover on its own: the backfill only re-indexes each channel's *recent* page, so an
// edit to an older message, or any deletion, would otherwise leave stale content (and a stale
// embedding) in the index indefinitely. Also the edit path for the Announcement Forwarder: when a
// previously-forwarded source announcement is edited, its translation is updated in place.
//
// - MESSAGE_UPDATE → re-run the same knowledge-channel index upsert as a new message (the upsert
//   drops the stored embedding when the text actually changed so it gets re-embedded), and re-run the
//   forwarder's update check (a no-op unless this message was actually forwarded and its text changed).
// - MESSAGE_DELETE / MESSAGE_DELETE_BULK → remove the row(s) from the AI-chat index.
//
// Uses GuildMessages intent (already enabled). MaybeIndexIncomingAsync guards against the bot's own
// messages, so an edit to a webhook/crossposted announcement is re-indexed (matching the create path)
// while the bot's own streamed-message edits are not. Only one class implements
// IMessageUpdateGatewayHandler by design (mirrors AiChatMessageHandler's single-handler-per-event
// convention for MESSAGE_CREATE) — a new update-driven behavior is added here, not as a second handler.
public class AiChatIndexReconcileHandler(IServiceScopeFactory scopeFactory, ILogger<AiChatIndexReconcileHandler> logger)
    : IMessageUpdateGatewayHandler, IMessageDeleteGatewayHandler, IMessageDeleteBulkGatewayHandler
{
    public async ValueTask HandleAsync(Message message)
    {
        if (message.GuildId is not { } guildId)
            return;

        using var scope = scopeFactory.CreateScope();

        try
        {
            var index = scope.ServiceProvider.GetRequiredService<AiChatIndexService>();
            await index.MaybeIndexIncomingAsync(message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AiChat index reconcile (edit) failed for message {MessageId}", message.Id);
        }

        try
        {
            var forwarder = scope.ServiceProvider.GetRequiredService<AnnouncementForwarderService>();
            await forwarder.MaybeUpdateForwardAsync(guildId, message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Announcement forward update failed for message {MessageId}", message.Id);
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
