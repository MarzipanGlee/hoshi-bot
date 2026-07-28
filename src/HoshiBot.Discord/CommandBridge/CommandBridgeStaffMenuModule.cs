using HoshiBot.Discord.Alerts;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.CommandBridge;

// User-select steps for the staff bridge's shield flows. Both selects live on this module's
// own ephemeral wizard message (posted by CommandBridgeStaffButtonModule), so the mute step
// edits it in place; the report step opens a modal against it.
public class CommandBridgeStaffMenuModule(AlertService alertService, EmbedBranding embedBranding)
    : ComponentInteractionModule<UserMenuInteractionContext>
{
    // All strings come from the message catalog (Msg.Bridge); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    // Shield report: member chosen → ask for the station system in a modal, carrying the
    // target + variant in the modal custom id.
    [ComponentInteraction("staff-shield-target")]
    public InteractionCallbackProperties<ModalProperties> ShieldReportTarget(string variant)
    {
        var target = Context.SelectedValues[0];
        return InteractionCallback.Modal(new ModalProperties($"staff-shield-modal:{target.Id}:{variant}", Msg.Bridge.StaffShieldTitle(Lang),
        [
            new LabelProperties(Msg.Bridge.LocationLabel(Lang),
                new TextInputProperties("system", TextInputStyle.Short) { Placeholder = Msg.Bridge.SystemPlaceholder(Lang), Required = true }),
        ]));
    }

    // Mute management: member chosen → show current state with enable/disable buttons.
    [ComponentInteraction("staff-shield-mute-target")]
    public Task ShieldMuteTarget() =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var target = Context.SelectedValues[0];
            var muted = await alertService.GetShieldMutedAsync(Context.Guild!.Id, target.Id);

            var status = muted
                ? Msg.Bridge.StaffMuteStateOn(Lang)
                : Msg.Bridge.StaffMuteStateOff(Lang);

            var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id,
                Msg.Bridge.StaffMuteStatus(Lang, $"<@{target.Id}>", status),
                title: Msg.Bridge.StaffMuteTitle(Lang));

            return m =>
            {
                m.Embeds = [embed];
                m.Components =
                [
                    new ActionRowProperties(
                    [
                        new ButtonProperties($"staff-shield-mute-set:{target.Id}:on", Msg.Bridge.StaffMuteEnableButton(Lang), EmojiProperties.Standard("🔕"), ButtonStyle.Primary) { Disabled = muted },
                        new ButtonProperties($"staff-shield-mute-set:{target.Id}:off", Msg.Bridge.StaffMuteDisableButton(Lang), EmojiProperties.Standard("🔔"), ButtonStyle.Secondary) { Disabled = !muted },
                    ]),
                ];
            };
        });
}
