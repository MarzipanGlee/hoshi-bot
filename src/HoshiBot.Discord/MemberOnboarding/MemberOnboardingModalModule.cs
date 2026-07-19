using NetCord;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.MemberOnboarding;

// Submit handler for the "type your in-game name" modal opened from the onboarding DM. The modal was
// opened from a component on the bot's own DM message, so ModifyDelayedResponseAsync edits that same
// message in place (Discord resolves it against the originating message — see CLAUDE.md).
public class MemberOnboardingModalModule(MemberOnboardingService onboarding) : ComponentInteractionModule<ModalInteractionContext>
{
    // player-link-name-modal:{reviewId}
    [ComponentInteraction(MemberOnboardingService.NameModalId)]
    public Task SubmitName(int reviewId) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var typedName = TextInputValues().GetValueOrDefault(MemberOnboardingService.NameInputId) ?? "";
            var reply = await onboarding.ResolveByNameAsync(reviewId, Context.User.Id, typedName, CancellationToken.None);
            return m => { m.Content = reply; m.Embeds = []; m.Components = []; };
        });

    private Dictionary<string, string> TextInputValues() =>
        Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .ToDictionary(i => i.CustomId, i => i.Value);
}
