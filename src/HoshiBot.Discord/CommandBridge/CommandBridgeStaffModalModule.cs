using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.CommandBridge;

// Modal submit for the staff shield-loss report: creates the reminder for the chosen member
// with an expiration derived from the reported variant. The modal was opened from the
// user-select menu on this flow's own ephemeral wizard message, so the result edits that
// message in place (never the shared hub).
public class CommandBridgeStaffModalModule(AlertService alertService, EmbedBranding embedBranding)
    : ComponentInteractionModule<ModalInteractionContext>
{
    [ComponentInteraction("staff-shield-modal")]
    public async Task<InteractionCallbackProperties<MessageOptions>> SubmitShieldReport(ulong targetUserId, string variant)
    {
        var system = TextInputValues().GetValueOrDefault("system") ?? "";
        var parsedVariant = Enum.TryParse<ShieldLossVariant>(variant, out var v) ? v : ShieldLossVariant.Manual;

        var result = await alertService.ReportStaffShieldLossAsync(Context.Guild!.Id, targetUserId, system, parsedVariant);

        var embed = new EmbedProperties
        {
            Title = "Schildverlust melden",
            Description = result,
            Color = EmbedBranding.BotColor,
            Author = await embedBranding.BuildAuthorAsync(Context.Guild!.Id),
            Footer = embedBranding.BuildFooter(Context.Guild!.Id),
        };
        return InteractionCallback.ModifyMessage(m => { m.Embeds = [embed]; m.Components = []; });
    }

    private Dictionary<string, string> TextInputValues() =>
        Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .ToDictionary(i => i.CustomId, i => i.Value);
}
