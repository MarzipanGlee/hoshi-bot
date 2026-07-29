using HoshiBot.Data;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.StfcNews;

public class StfcNewsModalModule(StfcNewsService service, EmbedBranding embedBranding, LanguageResolver languageResolver) : ComponentInteractionModule<ModalInteractionContext>
{
    // Personal ephemeral reply, not an edit to the shared message — see StfcNewsButtonModule.
    // Confirm for why (an immediate ack, then edited with the real outcome once
    // SubmitDateAsync's DB work completes, kept fully independent of the shared message's own
    // separate direct-REST update).
    [ComponentInteraction("stfc-news-date-modal")]
    public async Task SubmitDate(int postId)
    {
        // Everything rendered here (parse error and outcome alike) is ephemeral to the
        // submitting admin — their language.
        var lang = await languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

        // Permissive per-language parsing (DateInput): the resolved language's convention plus
        // the German dd.MM.yyyy and ISO fallbacks — same chain the Absences modals use.
        var dateText = TextInputValues().GetValueOrDefault("event-date");
        if (!DateInput.TryParseDate(dateText, lang, out var date))
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(
                EphemeralReply.Of(Msg.News.DateParseError(lang))));
            return;
        }

        await Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var outcome = await service.SubmitDateAsync(postId, date, Context.User.Id);
            return outcome switch
            {
                StfcNewsActionOutcome.NotFound => Msg.News.PostNotFound(lang),
                StfcNewsActionOutcome.AlreadyResolved => Msg.News.AlreadyConfirmed(lang),
                _ => Msg.News.DateSubmitted(lang, date),
            };
        });
    }

    private Dictionary<string, string> TextInputValues() =>
        Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .ToDictionary(i => i.CustomId, i => i.Value);
}
