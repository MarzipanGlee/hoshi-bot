using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.StfcNews;

public class StfcNewsModalModule(StfcNewsService service) : ComponentInteractionModule<ModalInteractionContext>
{
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
                EphemeralReply.Of("Could not read that date. Format: DD.MM.YYYY.")));
            return;
        }

        await Context.Interaction.SendDelayedResponseAsync(async () =>
        {
            var outcome = await service.SubmitDateAsync(postId, date, Context.User.Id);
            return outcome switch
            {
                StfcNewsActionOutcome.NotFound => "This post could no longer be found.",
                StfcNewsActionOutcome.AlreadyResolved => "This event date has already been confirmed.",
                _ => $"Thanks — your date ({date:dd.MM.yyyy}) has been submitted. Other admins can now confirm it.",
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
