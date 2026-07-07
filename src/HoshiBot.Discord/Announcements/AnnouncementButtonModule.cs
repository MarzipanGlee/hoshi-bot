using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Announcements;

public class AnnouncementButtonModule(AnnouncementService announcementService, GatewayClient gatewayClient, GuildFeatureService featureService)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    // All four Publish buttons and Cancel live on AnnouncementMessageCommandModule.Preview's
    // own ephemeral message, so ModifyMessage is safe here — never the public hub.
    [ComponentInteraction("announcement-publish-normal")]
    public Task<InteractionCallbackProperties<MessageOptions>> PublishNormal(ulong channelId, ulong messageId) =>
        PublishAsync(channelId, messageId, AnnouncementSeverity.Normal);

    [ComponentInteraction("announcement-publish-elevated")]
    public Task<InteractionCallbackProperties<MessageOptions>> PublishElevated(ulong channelId, ulong messageId) =>
        PublishAsync(channelId, messageId, AnnouncementSeverity.Elevated);

    [ComponentInteraction("announcement-publish-high")]
    public Task<InteractionCallbackProperties<MessageOptions>> PublishHigh(ulong channelId, ulong messageId) =>
        PublishAsync(channelId, messageId, AnnouncementSeverity.High);

    [ComponentInteraction("announcement-publish-direct")]
    public Task<InteractionCallbackProperties<MessageOptions>> PublishDirect(ulong channelId, ulong messageId) =>
        PublishAsync(channelId, messageId, AnnouncementSeverity.Direct);

    [ComponentInteraction("announcement-cancel")]
    public InteractionCallbackProperties<MessageOptions> Cancel() =>
        InteractionCallback.ModifyMessage(m => { m.Content = "Verworfen."; m.Embeds = []; m.Components = []; });

    [ComponentInteraction("announcement-read")]
    public async Task<InteractionMessageProperties> MarkRead(int announcementId)
    {
        var (wasNew, count) = await announcementService.MarkReadAsync(announcementId, Context.Guild!.Id, Context.User.Id);

        try
        {
            await gatewayClient.Rest.ModifyMessageAsync(Context.Channel.Id, Context.Message.Id,
                m => m.Components = [new ActionRowProperties([AnnouncementService.ReadButton(announcementId, count)])]);
        }
        catch (RestException)
        {
            // The periodic AnnouncementCounterRefreshJob will pick this up if the inline
            // edit fails (e.g. transient rate limit) — not worth failing the interaction for.
        }

        return EphemeralReply.Of(wasNew ? "Danke, deine Lesebestätigung wurde erfasst." : "Du hast diese Ankündigung bereits bestätigt.");
    }

    private async Task<InteractionCallbackProperties<MessageOptions>> PublishAsync(ulong channelId, ulong messageId, AnnouncementSeverity severity)
    {
        if (!await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.Announcements))
        {
            var disabledMessage = GuildFeatureService.DisabledMessage(GuildFeature.Announcements);
            return InteractionCallback.ModifyMessage(m => { m.Content = disabledMessage; m.Embeds = []; m.Components = []; });
        }

        // Re-fetching live (rather than carrying the draft's content in the custom-id,
        // which is far too small for a full announcement body) means an edit made
        // between preview and publish is naturally picked up.
        var draft = await gatewayClient.Rest.GetMessageAsync(channelId, messageId);
        var result = await announcementService.PublishAsync(Context.Guild!.Id, draft, severity, Context.User.Id);
        return InteractionCallback.ModifyMessage(m => { m.Content = result; m.Embeds = []; m.Components = []; });
    }
}
