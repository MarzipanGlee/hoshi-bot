using HoshiBot.Data;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.RoeViolations;

public class RoeViolationUserMenuModule(LanguageResolver languageResolver) : ComponentInteractionModule<UserMenuInteractionContext>
{
    // Async so the modal's labels can render in the acting user's resolved language — modals
    // can't be deferred, but the cached resolver lookup is well within Discord's 3s window.
    [ComponentInteraction("roe-violation-other-target")]
    public async Task<InteractionCallbackProperties<ModalProperties>> ReportRoeViolationOtherTarget()
    {
        var lang = await languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);
        var target = Context.SelectedValues[0];
        return InteractionCallback.Modal(RoeViolationService.Modal("other", target.Id, lang));
    }
}
