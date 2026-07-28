using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.CommandBridge;

// Staff bridge "Beta-Tests verwalten": self-service toggle of the caller's own beta-tester
// role. Not gated by a GuildFeature (it only ever affects the clicking user's own role).
public class CommandBridgeStaffBetaModule(BetaTesterService betaTesterService, EmbedBranding embedBranding)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    // All strings come from the message catalog (Msg.Bridge); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    [ComponentInteraction("staff-beta-tests")]
    public async Task<InteractionMessageProperties> Prompt()
    {
        var (configured, hasRole) = await betaTesterService.GetStatusAsync(Context.Guild!.Id, Context.User.Id);
        if (!configured)
            return EphemeralReply.Of(Msg.Bridge.BetaRoleNotConfigured(Lang));

        var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id,
            Msg.Bridge.BetaStatus(Lang, hasRole ? Msg.Bridge.BetaOn(Lang) : Msg.Bridge.BetaOff(Lang)),
            title: Msg.Bridge.BetaTitle(Lang));

        return new InteractionMessageProperties
        {
            Embeds = [embed],
            Flags = MessageFlags.Ephemeral,
            Components =
            [
                new ActionRowProperties(
                [
                    new ButtonProperties("staff-beta-tests-set:on", Msg.Bridge.BetaEnableButton(Lang), EmojiProperties.Standard("▶️"), ButtonStyle.Primary) { Disabled = hasRole },
                    new ButtonProperties("staff-beta-tests-set:off", Msg.Bridge.BetaDisableButton(Lang), EmojiProperties.Standard("⏹️"), ButtonStyle.Secondary) { Disabled = !hasRole },
                ]),
            ],
        };
    }

    // Buttons live on this module's own ephemeral message — ModifyMessage is safe.
    [ComponentInteraction("staff-beta-tests-set")]
    public Task Set(string action) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var result = await betaTesterService.SetAsync(Context.Guild!.Id, Context.User.Id, action == "on");
            var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, result, title: Msg.Bridge.BetaTitle(Lang));
            return m => { m.Embeds = [embed]; m.Components = []; };
        });
}
