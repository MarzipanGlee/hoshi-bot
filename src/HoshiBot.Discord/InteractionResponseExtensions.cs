using NetCord;
using NetCord.Rest;

namespace HoshiBot.Discord;

// Discord invalidates an interaction that isn't acknowledged within ~3 seconds, so any handler
// doing non-trivial work (DB writes, a REST call, anything not near-instant) must ack immediately
// and edit the response afterwards — never build a single response only at the end. These helpers
// capture that pattern once so every handler does it identically (see CLAUDE.md "ack immediately
// and edit afterward").
//
// Two ack flavors, matching the two contexts allowed by CLAUDE.md:
//   - Send*  → a NEW ephemeral "Processing" reply, personal to the clicking user and independent
//              of whatever message the component sits on. Use for buttons on shared/persistent
//              messages, slash/message commands, and any fresh reply.
//   - Modify* → edits the originating message in place. Use ONLY for a component inside an
//               already-ephemeral wizard message; never a shared/persistent one.
public static class InteractionResponseExtensions
{
    private const string Placeholder = "⏳ Processing...";

    // New ephemeral placeholder → run work → edit the reply with the result text.
    public static async Task SendDelayedResponseAsync(this Interaction interaction, Func<Task<string>> work)
    {
        await interaction.SendResponseAsync(InteractionCallback.Message(EphemeralReply.Of(Placeholder)));
        var content = await work();
        await interaction.ModifyResponseAsync(m => m.Content = content);
    }

    // Same new-ephemeral ack, but the final edit is an arbitrary MessageOptions mutation
    // (embeds/components) rather than plain content. Distinct name (not an overload) so the
    // returned `m => {…}` lambda target-types cleanly.
    public static async Task SendDelayedEditAsync(this Interaction interaction, Func<Task<Action<MessageOptions>>> work)
    {
        await interaction.SendResponseAsync(InteractionCallback.Message(EphemeralReply.Of(Placeholder)));
        var edit = await work();
        await interaction.ModifyResponseAsync(edit);
    }

    // In-place ack (edit the originating wizard message to the placeholder, clearing its
    // embeds/components) → run work → apply the final edit. Wizard-step use only.
    public static async Task ModifyDelayedResponseAsync(this Interaction interaction, Func<Task<Action<MessageOptions>>> work)
    {
        await interaction.SendResponseAsync(InteractionCallback.ModifyMessage(m =>
        {
            m.Content = Placeholder;
            m.Embeds = [];
            m.Components = [];
        }));
        var edit = await work();
        await interaction.ModifyResponseAsync(edit);
    }
}
