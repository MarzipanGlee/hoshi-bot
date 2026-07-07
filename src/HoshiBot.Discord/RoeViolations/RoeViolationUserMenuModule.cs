using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.RoeViolations;

public class RoeViolationUserMenuModule : ComponentInteractionModule<UserMenuInteractionContext>
{
    [ComponentInteraction("roe-violation-other-target")]
    public InteractionCallbackProperties<ModalProperties> ReportRoeViolationOtherTarget()
    {
        var target = Context.SelectedValues[0];
        return InteractionCallback.Modal(RoeViolationService.Modal("other", target.Id));
    }
}
