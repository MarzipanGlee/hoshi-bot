using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Announcements;

public class AnnouncementButtonModule(AnnouncementService announcementService, GatewayClient gatewayClient, GuildFeatureService featureService, GuildAllianceService allianceService)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    // All four Publish buttons and Cancel live on AnnouncementMessageCommandModule.Preview's
    // own ephemeral message, so ModifyMessage is safe here — never the public hub.
    [ComponentInteraction("announcement-publish-normal")]
    public Task PublishNormal(ulong channelId, ulong messageId, string audience) =>
        Context.Interaction.ModifyDelayedResponseAsync(() => PublishAsync(channelId, messageId, audience, AnnouncementSeverity.Normal));

    [ComponentInteraction("announcement-publish-elevated")]
    public Task PublishElevated(ulong channelId, ulong messageId, string audience) =>
        Context.Interaction.ModifyDelayedResponseAsync(() => PublishAsync(channelId, messageId, audience, AnnouncementSeverity.Elevated));

    [ComponentInteraction("announcement-publish-high")]
    public Task PublishHigh(ulong channelId, ulong messageId, string audience) =>
        Context.Interaction.ModifyDelayedResponseAsync(() => PublishAsync(channelId, messageId, audience, AnnouncementSeverity.High));

    [ComponentInteraction("announcement-publish-direct")]
    public Task PublishDirect(ulong channelId, ulong messageId, string audience) =>
        Context.Interaction.ModifyDelayedResponseAsync(() => PublishAsync(channelId, messageId, audience, AnnouncementSeverity.Direct));

    [ComponentInteraction("announcement-cancel")]
    public InteractionCallbackProperties<MessageOptions> Cancel() =>
        InteractionCallback.ModifyMessage(m => { m.Content = "Verworfen."; m.Embeds = []; m.Components = []; });

    [ComponentInteraction("announcement-pick-audience")]
    public InteractionCallbackProperties<MessageOptions> PickAudience(ulong channelId, ulong messageId, string audience) =>
        InteractionCallback.ModifyMessage(BuildSeverityPromptModifier(channelId, messageId, Enum.Parse<GuildAudience>(audience)));

    [ComponentInteraction("announcement-read")]
    public Task MarkRead(int announcementId) =>
        Context.Interaction.SendDelayedResponseAsync(async () =>
        {
            var (wasNew, count) = await announcementService.MarkReadAsync(announcementId, Context.Guild!.Id, Context.User.Id);

            try
            {
                // The shared announcement message (public) is edited via a separate direct REST
                // call, kept independent of this personal ephemeral ack.
                await gatewayClient.Rest.ModifyMessageAsync(Context.Channel.Id, Context.Message.Id,
                    m => m.Components = [new ActionRowProperties([AnnouncementService.ReadButton(announcementId, count)])]);
            }
            catch (RestException)
            {
                // The periodic AnnouncementCounterRefreshJob will pick this up if the inline
                // edit fails (e.g. transient rate limit) — not worth failing the interaction for.
            }

            return wasNew ? "Danke, deine Lesebestätigung wurde erfasst." : "Du hast diese Ankündigung bereits bestätigt.";
        });

    private async Task<Action<MessageOptions>> PublishAsync(ulong channelId, ulong messageId, string audience, AnnouncementSeverity severity)
    {
        var parsedAudience = Enum.Parse<GuildAudience>(audience);
        // Phase 1: the Alliance audience maps to the guild's primary linked alliance.
        var guildAllianceId = parsedAudience == GuildAudience.Alliance
            ? await allianceService.GetPrimaryIdAsync(Context.Guild!.Id)
            : null;
        if (parsedAudience == GuildAudience.Alliance && guildAllianceId is null
            || !await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.Announcements, parsedAudience, guildAllianceId))
        {
            var disabledMessage = GuildFeatureService.DisabledMessage(GuildFeature.Announcements);
            return m => { m.Content = disabledMessage; m.Embeds = []; m.Components = []; };
        }

        // Re-fetching live (rather than carrying the draft's content in the custom-id,
        // which is far too small for a full announcement body) means an edit made
        // between preview and publish is naturally picked up.
        var draft = await gatewayClient.Rest.GetMessageAsync(channelId, messageId);
        var result = await announcementService.PublishAsync(Context.Guild!.Id, parsedAudience, guildAllianceId, draft, severity, Context.User.Id);
        return m => { m.Content = result; m.Embeds = []; m.Components = []; };
    }

    // Shared with AnnouncementMessageCommandModule.Preview, which needs the same prompts
    // but isn't itself a ComponentInteractionModule (a different NetCord module base), so
    // can't host the "announcement-pick-audience"/publish button handlers.
    internal static InteractionMessageProperties BuildAudiencePrompt(RestMessage draft)
    {
        var idPart = $"{draft.ChannelId}:{draft.Id}";
        var (title, body) = AnnouncementService.ParseDraft(draft.Content);

        return new InteractionMessageProperties
        {
            Content = "Für welche Zielgruppe ist diese Ankündigung?",
            Embeds = [BuildPreviewEmbed(title, body, draft)],
            Flags = MessageFlags.Ephemeral,
            Components =
            [
                new ActionRowProperties(GuildFeatureAudiences.EnumerateFlags(GuildFeatureAudiences.RelevantAudiences(GuildFeature.Announcements))
                    .Select(a => new ButtonProperties($"announcement-pick-audience:{idPart}:{a}", GuildFeatureService.AudienceLabel(a), ButtonStyle.Primary))
                    .Append(new ButtonProperties("announcement-cancel", "Abbrechen", ButtonStyle.Secondary))),
            ],
        };
    }

    internal static InteractionMessageProperties BuildSeverityPrompt(RestMessage draft, GuildAudience audience)
    {
        var idPart = $"{draft.ChannelId}:{draft.Id}:{audience}";
        var (title, body) = AnnouncementService.ParseDraft(draft.Content);

        return new InteractionMessageProperties
        {
            Content = "Vorschau — wähle die Alarmstufe zum Veröffentlichen, oder brich ab.",
            Embeds = [BuildPreviewEmbed(title, body, draft)],
            Flags = MessageFlags.Ephemeral,
            Components = [BuildSeverityButtonRow(idPart)],
        };
    }

    // PickAudience only has channelId/messageId (from its own custom-id), not the
    // RestMessage draft BuildSeverityPrompt wants — re-fetching isn't worth it here since
    // ModifyMessage's action just needs to replace the button row, not rebuild the embed.
    private static Action<MessageOptions> BuildSeverityPromptModifier(ulong channelId, ulong messageId, GuildAudience audience) => m =>
    {
        m.Content = "Vorschau — wähle die Alarmstufe zum Veröffentlichen, oder brich ab.";
        m.Components = [BuildSeverityButtonRow($"{channelId}:{messageId}:{audience}")];
    };

    private static ActionRowProperties BuildSeverityButtonRow(string idPart) => new(
    [
        new ButtonProperties($"announcement-publish-normal:{idPart}", "Normal", EmojiProperties.Standard("🟩"), ButtonStyle.Success),
        new ButtonProperties($"announcement-publish-elevated:{idPart}", "Erhöht", EmojiProperties.Standard("🟨"), ButtonStyle.Primary),
        new ButtonProperties($"announcement-publish-high:{idPart}", "Hoch", EmojiProperties.Standard("🟥"), ButtonStyle.Danger),
        new ButtonProperties($"announcement-publish-direct:{idPart}", "Direkt", EmojiProperties.Standard("🟦"), ButtonStyle.Primary),
        new ButtonProperties("announcement-cancel", "Abbrechen", ButtonStyle.Secondary),
    ]);

    private static EmbedProperties BuildPreviewEmbed(string title, string body, RestMessage draft) => new()
    {
        Title = string.IsNullOrWhiteSpace(title) ? "*(kein Titel)*" : title,
        Description = string.IsNullOrWhiteSpace(body) ? "*(kein Text)*" : body,
        Footer = draft.Attachments.Count > 0
            ? new EmbedFooterProperties { Text = $"{draft.Attachments.Count} Anhang/Anhänge" }
            : null,
    };
}
