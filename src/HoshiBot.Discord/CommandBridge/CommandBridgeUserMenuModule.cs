using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.CommandBridge;

public class CommandBridgeUserMenuModule(EmbedBranding embedBranding) : ComponentInteractionModule<UserMenuInteractionContext>
{
    // Modals can't have radio buttons/selects, so the Home/Enemy server choice is a
    // button step here rather than a modal field — see CommandBridgeButtonModule for
    // the buttons that actually open the modal. This always follows raid-report's own
    // ephemeral message, so editing it in place is safe (never the public hub).
    [ComponentInteraction("raid-report-target")]
    public async Task<InteractionCallbackProperties<MessageOptions>> ReportRaidTarget()
    {
        var target = Context.SelectedValues[0];
        var guildId = Context.Guild!.Id;

        var embed = await embedBranding.BuildBrandedAsync(guildId, "Auf welchem Server wird die Station geraidet?");

        return InteractionCallback.ModifyMessage(m =>
        {
            m.Embeds = [embed];
            m.Components =
            [
                new ActionRowProperties(
                [
                    new ButtonProperties($"raid-report-location-home:{target.Id}", "Home Server", EmojiProperties.Standard("🏠"), ButtonStyle.Primary),
                    new ButtonProperties($"raid-report-location-enemy:{target.Id}", "Enemy Server", EmojiProperties.Standard("⚔️"), ButtonStyle.Danger),
                ]),
            ];
        });
    }
}
