using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.StfcNews;

public class StfcNewsModalModule(StfcNewsService service, EmbedBranding embedBranding) : ComponentInteractionModule<ModalInteractionContext>
{
    // All strings come from the message catalog (Msg.News); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    // Personal ephemeral reply, not an edit to the shared message — see StfcNewsButtonModule.
    // Confirm for why (an immediate ack, then edited with the real outcome once
    // SubmitDateAsync's DB work completes, kept fully independent of the shared message's own
    // separate direct-REST update).
    [ComponentInteraction("stfc-news-date-modal")]
    public async Task SubmitDate(int postId)
    {
        var dateText = TextInputValues().GetValueOrDefault("event-date");
        if (!DateOnly.TryParseExact(dateText?.Trim(), "dd.MM.yyyy", out var date))
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(
                EphemeralReply.Of(Msg.News.DateParseError(Lang))));
            return;
        }

        await Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var outcome = await service.SubmitDateAsync(postId, date, Context.User.Id);
            return outcome switch
            {
                StfcNewsActionOutcome.NotFound => Msg.News.PostNotFound(Lang),
                StfcNewsActionOutcome.AlreadyResolved => Msg.News.AlreadyConfirmed(Lang),
                _ => Msg.News.DateSubmitted(Lang, date),
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
