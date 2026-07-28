using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.MemberOnboarding;

// Buttons on the MemberOnboarding player-confirmation DM. Both live on the bot's own DM message, so
// editing in place (ModifyMessage / ModifyDelayedResponseAsync) is safe — see CLAUDE.md.
public class MemberOnboardingButtonModule(MemberOnboardingService onboarding) : ComponentInteractionModule<ButtonInteractionContext>
{
    // All strings come from the message catalog (Msg.Onboarding); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    // player-link-confirm:{reviewId}:{playerId} — the member accepted the bot's guess.
    [ComponentInteraction(MemberOnboardingService.ConfirmButtonId)]
    public Task Confirm(int reviewId, int playerId) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var reply = await onboarding.ConfirmAsync(reviewId, playerId, Context.User.Id, CancellationToken.None);
            return m => { m.Content = reply; m.Embeds = []; m.Components = []; };
        });

    // player-link-name:{reviewId} — the member wants to type their in-game name. Opening a modal must
    // be the direct response to the button (it can't be deferred), so this returns the modal itself.
    [ComponentInteraction(MemberOnboardingService.NameButtonId)]
    public async Task EnterName(int reviewId) =>
        await Context.Interaction.SendResponseAsync(InteractionCallback.Modal(new ModalProperties(
            $"{MemberOnboardingService.NameModalId}:{reviewId}", Msg.Onboarding.NameModalTitle(Lang),
            [
                new LabelProperties(Msg.Onboarding.NameInputLabel(Lang),
                    new TextInputProperties(MemberOnboardingService.NameInputId, TextInputStyle.Short)
                    {
                        Required = true,
                        MaxLength = 100,
                        Placeholder = Msg.Onboarding.NameInputPlaceholder(Lang),
                    }),
            ])));
}
