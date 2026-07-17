using HoshiBot.Domain.Entities;
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
    // Shield report: member chosen → ask for the station system in a modal, carrying the
    // target + variant in the modal custom id.
    [ComponentInteraction("staff-shield-target")]
    public InteractionCallbackProperties<ModalProperties> ShieldReportTarget(string variant)
    {
        var target = Context.SelectedValues[0];
        return InteractionCallback.Modal(new ModalProperties($"staff-shield-modal:{target.Id}:{variant}", "Schildverlust melden",
        [
            new LabelProperties("Standort",
                new TextInputProperties("system", TextInputStyle.Short) { Placeholder = "System eingeben", Required = true }),
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
                ? "EIN — öffentliche Benachrichtigungen bei Schildablauf deaktiviert"
                : "AUS — öffentliche Benachrichtigungen bei Schildablauf aktiviert";

            var embed = new EmbedProperties
            {
                Title = "Öffentliche Schildablaufwarnungen verwalten",
                Description = $"Die Stummschaltung der Schildablaufwarnungen für <@{target.Id}> ist aktuell:\n\n- **{status}**",
                Color = EmbedBranding.BotColor,
                Author = await embedBranding.BuildAuthorAsync(Context.Guild!.Id),
                Footer = embedBranding.BuildFooter(Context.Guild!.Id),
            };

            return m =>
            {
                m.Embeds = [embed];
                m.Components =
                [
                    new ActionRowProperties(
                    [
                        new ButtonProperties($"staff-shield-mute-set:{target.Id}:on", "Stummschaltung aktivieren", EmojiProperties.Standard("🔕"), ButtonStyle.Primary) { Disabled = muted },
                        new ButtonProperties($"staff-shield-mute-set:{target.Id}:off", "Stummschaltung deaktivieren", EmojiProperties.Standard("🔔"), ButtonStyle.Secondary) { Disabled = !muted },
                    ]),
                ];
            };
        });
}
