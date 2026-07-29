using HoshiBot.Data;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.MemberOnboarding;

// Buttons on the MemberOnboarding player-confirmation DM. Both live on the bot's own DM message, so
// editing in place (ModifyMessage / ModifyDelayedResponseAsync) is safe — see CLAUDE.md.
public class MemberOnboardingButtonModule(MemberOnboardingService onboarding, LanguageResolver languageResolver) : ComponentInteractionModule<ButtonInteractionContext>
{
    // player-link-confirm:{reviewId}:{playerId} — the member accepted the bot's guess.
    [ComponentInteraction(MemberOnboardingService.ConfirmButtonId)]
    public Task Confirm(int reviewId, int playerId) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var reply = await onboarding.ConfirmAsync(reviewId, playerId, Context.User.Id, CancellationToken.None);
            return m => { m.Content = reply; m.Embeds = []; m.Components = []; };
        });

    // player-link-name:{reviewId} — the member wants to type their in-game name. Opening a modal must
    // be the direct response to the button (it can't be deferred), so this returns the modal itself —
    // rendered in the acting member's language (resolved first; clicked from a DM, so there's no
    // guild context to scope by, but the interaction locale covers it).
    [ComponentInteraction(MemberOnboardingService.NameButtonId)]
    public async Task EnterName(int reviewId)
    {
        var lang = await languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild?.Id);
        await Context.Interaction.SendResponseAsync(InteractionCallback.Modal(new ModalProperties(
            $"{MemberOnboardingService.NameModalId}:{reviewId}", Msg.Onboarding.NameModalTitle(lang),
            [
                new LabelProperties(Msg.Onboarding.NameInputLabel(lang),
                    new TextInputProperties(MemberOnboardingService.NameInputId, TextInputStyle.Short)
                    {
                        Required = true,
                        MaxLength = 100,
                        Placeholder = Msg.Onboarding.NameInputPlaceholder(lang),
                    }),
            ])));
    }
}
