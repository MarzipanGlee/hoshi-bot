using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Announcements;

public class AnnouncementButtonModule(AnnouncementService announcementService, GatewayClient gatewayClient, GuildFeatureService featureService, GuildAllianceService allianceService, EmbedBranding embedBranding,
    LanguageResolver languageResolver)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    // Wizard steps (audience prompt, publish confirm, cancel, publish result) render in the
    // reacting staff member's language. Only the published post and its Read button render in
    // the target channel's owning scope (resolved inside the service).
    private Task<Language> ActingUserLanguageAsync() =>
        languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

    // Publish and Cancel live on the prompt AnnouncementDraftService posted into the draft
    // channel, so ModifyMessage edits that prompt — never the public hub, and never the draft
    // itself. Leaving the outcome ("Published in #x" / "Discarded.") in place of the prompt is
    // deliberate: the prep channel keeps a record of what was published from which draft.
    [ComponentInteraction("announcement-publish")]
    public Task Publish(ulong channelId, ulong messageId, string audience, string severity) =>
        Context.Interaction.ModifyDelayedResponseAsync(() => PublishAsync(channelId, messageId, audience, Enum.Parse<AnnouncementSeverity>(severity)));

    [ComponentInteraction("announcement-cancel")]
    public async Task<InteractionCallbackProperties<MessageOptions>> Cancel()
    {
        var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, Msg.Announce.Discarded(await ActingUserLanguageAsync()));
        return InteractionCallback.ModifyMessage(m => { m.Content = ""; m.Embeds = [embed]; m.Components = []; });
    }

    [ComponentInteraction("announcement-pick-audience")]
    public async Task<InteractionCallbackProperties<MessageOptions>> PickAudience(ulong channelId, ulong messageId, string severity, string audience) =>
        InteractionCallback.ModifyMessage(BuildPublishPromptModifier(
            channelId, messageId, Enum.Parse<GuildAudience>(audience), Enum.Parse<AnnouncementSeverity>(severity), await ActingUserLanguageAsync()));

    [ComponentInteraction("announcement-read")]
    public Task MarkRead(int announcementId) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var lang = await ActingUserLanguageAsync();
            var (wasNew, count) = await announcementService.MarkReadAsync(announcementId, Context.Guild!.Id, Context.User.Id);

            try
            {
                // The shared announcement message (public) is edited via a separate direct REST
                // call, kept independent of this personal ephemeral ack — its Read button keeps
                // the post's own scope language, not the clicking user's.
                var postLang = await announcementService.PostLanguageAsync(announcementId, Context.Guild!.Id);
                await gatewayClient.Rest.ModifyMessageAsync(Context.Channel.Id, Context.Message.Id,
                    m => m.Components = [new ActionRowProperties([AnnouncementService.ReadButton(announcementId, count, postLang)])]);
            }
            catch (RestException)
            {
                // The periodic AnnouncementCounterRefreshJob will pick this up if the inline
                // edit fails (e.g. transient rate limit) — not worth failing the interaction for.
            }

            return wasNew ? Msg.Announce.ReadRecorded(lang) : Msg.Announce.AlreadyRead(lang);
        });

    private async Task<Action<MessageOptions>> PublishAsync(ulong channelId, ulong messageId, string audience, AnnouncementSeverity severity)
    {
        var (parsedAudience, guildAllianceId, scopeMissing) = await allianceService.ResolveScopeAsync(Context.Guild!.Id, audience);
        if (scopeMissing || !await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.Announcements, parsedAudience, guildAllianceId))
        {
            var disabledMessage = GuildFeatureService.DisabledMessage(GuildFeature.Announcements, await ActingUserLanguageAsync());
            return await embedBranding.BrandedEditAsync(Context.Guild!.Id, disabledMessage);
        }

        // Re-fetching live (rather than carrying the draft's content in the custom-id,
        // which is far too small for a full announcement body) means an edit made
        // between preview and publish is naturally picked up.
        var draft = await gatewayClient.Rest.GetMessageAsync(channelId, messageId);
        var result = await announcementService.PublishAsync(Context.Guild!.Id, parsedAudience, guildAllianceId, draft, severity, Context.User.Id);
        return await embedBranding.BrandedEditAsync(Context.Guild!.Id, result);
    }

    // Built for AnnouncementDraftService, which starts this flow from a draft-channel reaction —
    // not an interaction — so it posts a normal message rather than an ephemeral wizard. Lives
    // here, next to the handlers whose custom-ids these buttons carry. Never pings: the draft's
    // own text is quoted back into the preview embed, and a mention in it must not re-notify.
    internal static MessageProperties BuildAudiencePrompt(RestMessage draft, EmbedProperties preview, string commander, AnnouncementSeverity severity, Language lang)
    {
        var idPart = $"{draft.ChannelId}:{draft.Id}:{severity}";

        return new MessageProperties
        {
            Content = Msg.Announce.AudiencePrompt(lang),
            Embeds = [preview, BuildPreviewCard(commander, severity, lang)],
            AllowedMentions = AllowedMentionsProperties.None,
            MessageReference = MessageReferenceProperties.Reply(draft.Id, failIfNotExists: false),
            Components =
            [
                new ActionRowProperties(GuildFeatureAudiences.EnumerateFlags(GuildFeatureAudiences.RelevantAudiences(GuildFeature.Announcements))
                    .Select(a => new ButtonProperties($"announcement-pick-audience:{idPart}:{a}", GuildFeatureService.AudienceLabel(a, lang), ButtonStyle.Primary))
                    .Append(new ButtonProperties("announcement-cancel", Msg.Announce.CancelButton(lang), ButtonStyle.Secondary))),
            ],
        };
    }

    internal static MessageProperties BuildPublishPrompt(RestMessage draft, EmbedProperties preview, string commander, GuildAudience audience, AnnouncementSeverity severity, Language lang) =>
        new()
        {
            Content = null,
            Embeds = [preview, BuildPreviewCard(commander, severity, lang)],
            AllowedMentions = AllowedMentionsProperties.None,
            MessageReference = MessageReferenceProperties.Reply(draft.Id, failIfNotExists: false),
            Components = [BuildPublishButtonRow($"{draft.ChannelId}:{draft.Id}:{audience}:{severity}", severity, lang)],
        };

    // PickAudience only has channelId/messageId (from its own custom-id), not the
    // RestMessage draft BuildPublishPrompt wants — re-fetching isn't worth it here since
    // ModifyMessage's action just needs to replace the button row, not rebuild the embed.
    private static Action<MessageOptions> BuildPublishPromptModifier(ulong channelId, ulong messageId, GuildAudience audience, AnnouncementSeverity severity, Language lang) => m =>
    {
        // Only the button row changes on the audience step — the preview embeds above it are
        // already correct, and rebuilding them would mean re-fetching the draft for nothing.
        m.Content = null;
        m.Components = [BuildPublishButtonRow($"{channelId}:{messageId}:{audience}:{severity}", severity, lang)];
    };

    // The card under the preview: what staff are being asked to confirm, and what the severity they
    // reacted with will actually do. Legacy's second embed, which said the same two things — a
    // reaction is easy to mis-click, and "Elevated" alone doesn't tell you it pings a role.
    private static EmbedProperties BuildPreviewCard(string commander, AnnouncementSeverity severity, Language lang) => new()
    {
        Title = Msg.Announce.PreviewTitle(lang),
        Description = Msg.Announce.PreviewIntro(lang, commander),
        Fields =
        [
            new EmbedFieldProperties { Name = Msg.Announce.FieldSeverity(lang), Value = $"{AnnouncementSeverities.Emoji(severity)} {AnnouncementSeverities.Label(severity, lang)}" },
            new EmbedFieldProperties { Name = Msg.Announce.FieldSeverityExplanation(lang), Value = Msg.Announce.SeverityDescription(lang, severity) },
        ],
    };

    // One publish button, because the reaction already picked the severity — it carries that
    // severity's own emoji so the confirm step visibly matches the reaction that opened it.
    private static ActionRowProperties BuildPublishButtonRow(string idPart, AnnouncementSeverity severity, Language lang) => new(
    [
        new ButtonProperties($"announcement-publish:{idPart}", Msg.Announce.PublishButton(lang),
            EmojiProperties.Standard(AnnouncementSeverities.Emoji(severity)), ButtonStyle.Success),
        new ButtonProperties("announcement-cancel", Msg.Announce.CancelButton(lang), EmojiProperties.Standard(Icons.Error), ButtonStyle.Danger),
    ]);

}
