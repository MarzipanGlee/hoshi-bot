using HoshiBot.Discord.Announcements;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace HoshiBot.Host;

// The single MESSAGE_REACTION_ADD handler: staff clicking one of the 🟩 🟨 🟥 🟦 reactions the bot
// put on an announcement draft. Kept thin like AiChatMessageHandler — all the gating lives in
// AnnouncementDraftService, which sees every reaction in every guild and narrows by emoji and
// channel. Auto-registered by AddGatewayHandlers(typeof(Program).Assembly).
//
// Requires GatewayIntents.GuildMessageReactions (see Program.cs) — not a privileged intent, so no
// Developer Portal toggle, but without it this event simply never arrives.
public class AnnouncementDraftReactionHandler(IServiceScopeFactory scopeFactory, ILogger<AnnouncementDraftReactionHandler> logger)
    : IMessageReactionAddGatewayHandler
{
    public async ValueTask HandleAsync(MessageReactionAddEventArgs args)
    {
        // DM reactions carry no guild — nothing here is scoped to anything but a guild channel.
        if (args.GuildId is not { } guildId)
            return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var drafts = scope.ServiceProvider.GetRequiredService<AnnouncementDraftService>();
            await drafts.HandleDraftReactionAsync(guildId, args.ChannelId, args.MessageId, args.UserId, args.Emoji.Name, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Announcement draft reaction handling failed for message {MessageId}", args.MessageId);
        }
    }
}
