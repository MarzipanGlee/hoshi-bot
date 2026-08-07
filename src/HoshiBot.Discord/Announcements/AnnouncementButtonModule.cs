using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Announcements;

public class AnnouncementButtonModule(AnnouncementService announcementService, AnnouncementDraftService draftService, GatewayClient gatewayClient,
    GuildFeatureService featureService, GuildAllianceService allianceService, EmbedBranding embedBranding,
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
    // The standard flow used everywhere else: an ephemeral "working on it" goes back immediately,
    // then gets edited with the outcome. Publishing does real work (fetch the draft, post, write the
    // row, edit the button in) and Discord's three seconds don't cover it.
    [ComponentInteraction("announcement-publish")]
    public Task Publish(ulong channelId, ulong messageId, string audience, string severity, string target) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, () =>
            PublishAsync(channelId, messageId, audience, Enum.Parse<AnnouncementSeverity>(severity), target == TestTarget));

    // Cancelling removes the preview entirely rather than editing it into a tombstone: the draft
    // channel is a working surface, and a discarded preview left sitting under the draft it belongs
    // to is just noise between staff and the next attempt. The confirmation goes back ephemerally,
    // as legacy did — nobody but the person who clicked needs to know.
    //
    // The draft's four squares go back on, or the draft would be stranded: no reactions, no way to
    // publish it, and no sign of why.
    [ComponentInteraction("announcement-cancel")]
    public async Task<InteractionMessageProperties> Cancel(ulong channelId, ulong messageId)
    {
        var lang = await ActingUserLanguageAsync();

        await draftService.AddDraftReactionsAsync(Context.Guild!.Id, channelId, messageId);

        try
        {
            await gatewayClient.Rest.DeleteMessageAsync(Context.Channel.Id, Context.Message.Id);
        }
        catch (RestException)
        {
            // Someone deleted it first, or the channel went away — the reactions are restored
            // either way, which is the part that matters.
        }

        var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, Msg.Announce.Discarded(lang, CommanderName.Of(Context.User)));
        return new InteractionMessageProperties { Embeds = [embed], Flags = MessageFlags.Ephemeral };
    }

    [ComponentInteraction("announcement-pick-audience")]
    public async Task<InteractionCallbackProperties<MessageOptions>> PickAudience(ulong channelId, ulong messageId, string severity, string audience) =>
        InteractionCallback.ModifyMessage(BuildPublishPromptModifier(
            channelId, messageId, Enum.Parse<GuildAudience>(audience), Enum.Parse<AnnouncementSeverity>(severity),
            await TargetsAsync(Enum.Parse<GuildAudience>(audience)), await ActingUserLanguageAsync()));

    private async Task<(string? LiveName, string? TestName)> TargetsAsync(GuildAudience audience)
    {
        var (_, guildAllianceId, scopeMissing) = await allianceService.ResolveScopeAsync(Context.Guild!.Id, audience.ToString());
        return scopeMissing ? (null, null) : await announcementService.PublishTargetNamesAsync(Context.Guild!.Id, audience, guildAllianceId);
    }

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

    // The custom-id's last segment: which of the two destinations the clicked button meant.
    private const string TestTarget = "test";
    private const string LiveTarget = "live";

    private async Task<string> PublishAsync(ulong channelId, ulong messageId, string audience, AnnouncementSeverity severity, bool toTestChannel)
    {
        var (parsedAudience, guildAllianceId, scopeMissing) = await allianceService.ResolveScopeAsync(Context.Guild!.Id, audience);
        if (scopeMissing || !await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.Announcements, parsedAudience, guildAllianceId))
            return GuildFeatureService.DisabledMessage(GuildFeature.Announcements, await ActingUserLanguageAsync());

        // Re-fetching live (rather than carrying the draft's content in the custom-id,
        // which is far too small for a full announcement body) means an edit made
        // between preview and publish is naturally picked up.
        var draft = await gatewayClient.Rest.GetMessageAsync(channelId, messageId);
        var (published, message) = await announcementService.PublishAsync(Context.Guild!.Id, parsedAudience, guildAllianceId, draft, severity, Context.User.Id, toTestChannel);

        // Both the prompt and the draft it was built from have done their job. Leaving them turns
        // the prep channel into a pile of published drafts and dead confirm dialogs, and a leftover
        // draft still carrying reactions invites a second publish of the same announcement.
        if (published)
        {
            await TryDeleteAsync(Context.Channel.Id, Context.Message.Id);
            await TryDeleteAsync(channelId, messageId);
        }

        return message;
    }

    private async Task TryDeleteAsync(ulong channelId, ulong messageId)
    {
        try
        {
            await gatewayClient.Rest.DeleteMessageAsync(channelId, messageId);
        }
        catch (RestException)
        {
            // Already gone, or the channel is. The announcement is published either way, which is
            // the outcome the member is waiting on — this is tidying, not the job.
        }
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
                    .Append(new ButtonProperties($"announcement-cancel:{draft.ChannelId}:{draft.Id}", Msg.Announce.CancelButton(lang),
                        EmojiProperties.Standard(Icons.Cancel), ButtonStyle.Danger))),
            ],
        };
    }

    internal static MessageProperties BuildPublishPrompt(RestMessage draft, EmbedProperties preview, string commander, GuildAudience audience, AnnouncementSeverity severity,
        (string? LiveName, string? TestName) targets, Language lang) =>
        new()
        {
            Content = null,
            Embeds = [preview, BuildPreviewCard(commander, severity, lang)],
            AllowedMentions = AllowedMentionsProperties.None,
            MessageReference = MessageReferenceProperties.Reply(draft.Id, failIfNotExists: false),
            Components = [BuildPublishButtonRow(draft.ChannelId, draft.Id, audience, severity, targets, lang)],
        };

    // PickAudience only has channelId/messageId (from its own custom-id), not the
    // RestMessage draft BuildPublishPrompt wants — re-fetching isn't worth it here since
    // ModifyMessage's action just needs to replace the button row, not rebuild the embed.
    private static Action<MessageOptions> BuildPublishPromptModifier(ulong channelId, ulong messageId, GuildAudience audience, AnnouncementSeverity severity,
        (string? LiveName, string? TestName) targets, Language lang) => m =>
    {
        // Only the button row changes on the audience step — the preview embeds above it are
        // already correct, and rebuilding them would mean re-fetching the draft for nothing.
        m.Content = null;
        m.Components = [BuildPublishButtonRow(channelId, messageId, audience, severity, targets, lang)];
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

    // The publish button carries the reacted severity's own emoji, so the confirm step visibly
    // matches the reaction that opened it. With a test channel configured there are two of them,
    // as in legacy: the dry run first (secondary, so the real one stays the obvious green target),
    // then the live channel.
    //
    // Cancel uses ✖️ rather than ❌ — a red-on-red cross on the danger button is barely legible.
    private static ActionRowProperties BuildPublishButtonRow(ulong channelId, ulong messageId, GuildAudience audience, AnnouncementSeverity severity,
        (string? LiveName, string? TestName) targets, Language lang)
    {
        var idPart = $"{channelId}:{messageId}:{audience}:{severity}";
        var buttons = new List<ButtonProperties>();

        if (targets.TestName is { } testName)
        {
            buttons.Add(new ButtonProperties($"announcement-publish:{idPart}:{TestTarget}", Label(testName),
                EmojiProperties.Standard(Icons.Public), ButtonStyle.Primary));
        }

        buttons.Add(new ButtonProperties($"announcement-publish:{idPart}:{LiveTarget}", Label(targets.LiveName),
            EmojiProperties.Standard(Icons.Public), ButtonStyle.Success));
        buttons.Add(new ButtonProperties($"announcement-cancel:{channelId}:{messageId}",
            Msg.Announce.CancelButton(lang), EmojiProperties.Standard(Icons.Cancel), ButtonStyle.Danger));

        return new ActionRowProperties(buttons);

        // A channel the bot can't name (unconfigured, or missing from its cache) gets the plain
        // label rather than a made-up name.
        string Label(string? channelName) =>
            channelName is null ? Msg.Announce.PublishButton(lang) : Msg.Announce.PublishToButton(lang, channelName);
    }

}
