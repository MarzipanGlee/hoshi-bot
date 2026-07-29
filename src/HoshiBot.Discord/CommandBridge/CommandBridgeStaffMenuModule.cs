using HoshiBot.Data;
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
public class CommandBridgeStaffMenuModule(AlertService alertService, EmbedBranding embedBranding, LanguageResolver languageResolver)
    : ComponentInteractionModule<UserMenuInteractionContext>
{
    // Both steps are ephemeral to the acting staff member — their language.
    private Task<Language> ActingUserLanguageAsync() =>
        languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

    // Shield report: member chosen → ask for the station system in a modal, carrying the
    // target + variant in the modal custom id. Async so the modal renders in the acting user's
    // language — modals can't be deferred, but the cached resolver lookup is well within
    // Discord's 3s window.
    [ComponentInteraction("staff-shield-target")]
    public async Task<InteractionCallbackProperties<ModalProperties>> ShieldReportTarget(string variant)
    {
        var lang = await ActingUserLanguageAsync();
        var target = Context.SelectedValues[0];
        return InteractionCallback.Modal(new ModalProperties($"staff-shield-modal:{target.Id}:{variant}", Msg.Bridge.StaffShieldTitle(lang),
        [
            new LabelProperties(Msg.Bridge.LocationLabel(lang),
                new TextInputProperties("system", TextInputStyle.Short) { Placeholder = Msg.Bridge.SystemPlaceholder(lang), Required = true }),
        ]));
    }

    // Mute management: member chosen → show current state with enable/disable buttons.
    [ComponentInteraction("staff-shield-mute-target")]
    public Task ShieldMuteTarget() =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var lang = await ActingUserLanguageAsync();
            var target = Context.SelectedValues[0];
            var muted = await alertService.GetShieldMutedAsync(Context.Guild!.Id, target.Id);

            var status = muted
                ? Msg.Bridge.StaffMuteStateOn(lang)
                : Msg.Bridge.StaffMuteStateOff(lang);

            var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id,
                Msg.Bridge.StaffMuteStatus(lang, $"<@{target.Id}>", status),
                title: Msg.Bridge.StaffMuteTitle(lang));

            return m =>
            {
                m.Embeds = [embed];
                m.Components =
                [
                    new ActionRowProperties(
                    [
                        new ButtonProperties($"staff-shield-mute-set:{target.Id}:on", Msg.Bridge.StaffMuteEnableButton(lang), EmojiProperties.Standard("🔕"), ButtonStyle.Primary) { Disabled = muted },
                        new ButtonProperties($"staff-shield-mute-set:{target.Id}:off", Msg.Bridge.StaffMuteDisableButton(lang), EmojiProperties.Standard("🔔"), ButtonStyle.Secondary) { Disabled = !muted },
                    ]),
                ];
            };
        });
}
