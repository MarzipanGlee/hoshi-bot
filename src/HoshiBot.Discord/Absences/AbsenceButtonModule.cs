using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Absences;

public class AbsenceButtonModule(AbsenceService absenceService, EmbedBranding embedBranding) : ComponentInteractionModule<ButtonInteractionContext>
{
    // All strings come from the message catalog (Msg.Absence); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    // Every wizard step after the entry point edits the same ephemeral message via ModifyMessage
    // (the wizard experience the legacy bot had) instead of stacking a fresh ephemeral per step.
    private async Task<InteractionCallbackProperties<MessageOptions>> EphemeralEmbedModifyAsync(string description, IReadOnlyList<IMessageComponentProperties>? components = null, string? title = null)
    {
        var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, description, title: title);
        return InteractionCallback.ModifyMessage(m =>
        {
            m.Embeds = [embed];
            m.Components = components ?? [];
        });
    }

    // Fetching upcoming absences can take a moment under load, so — matching legacy's own
    // two-step "wird gesucht..." placeholder before the query, then editing the response
    // once it's done — this sends the loading state itself and edits it in place, rather
    // than returning a value for the framework to send only once everything is ready.
    [ComponentInteraction("absence-manage")]
    public Task ManageAbsences() =>
        Context.Interaction.SendDelayedEditAsync(async () =>
        {
            var own = await absenceService.GetOwnUpcomingAsync(Context.Guild!.Id, Context.User.Id);
            var hasOwn = own.Count > 0;

            // Edit/Delete are always shown, just disabled when there's nothing to act on yet —
            // matches the legacy bot's behavior instead of hiding the buttons outright.
            var buttons = new List<ButtonProperties>
            {
                new("absence-create", Msg.Absence.CreateTitle(Lang), EmojiProperties.Standard("➕"), ButtonStyle.Success),
                new ButtonProperties("absence-edit", Msg.Absence.EditTitle(Lang), EmojiProperties.Standard("✏️"), ButtonStyle.Primary) { Disabled = !hasOwn },
                new ButtonProperties("absence-delete", Msg.Absence.DeleteTitle(Lang), EmojiProperties.Standard("✖️"), ButtonStyle.Danger) { Disabled = !hasOwn },
            };

            var description = Msg.Absence.ManageIntro(Lang, CommanderName.Of(Context.User), AbsenceService.BuildOwnListText(own));

            var finalEmbed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, description, title: Msg.Absence.ManageTitle(Lang));

            return m =>
            {
                m.Embeds = [finalEmbed];
                m.Components = [new ActionRowProperties(buttons)];
            };
        });

    [ComponentInteraction("absence-create")]
    public Task<InteractionCallbackProperties<MessageOptions>> CreatePrompt() =>
        EphemeralEmbedModifyAsync(
            Msg.Absence.VisibilityPrompt(Lang, CommanderName.Of(Context.User)),
            [
                new ActionRowProperties(
                [
                    new ButtonProperties($"absence-create-vis:{AbsenceVisibility.Public}", Msg.Absence.PublicButton(Lang), EmojiProperties.Standard("📢"), ButtonStyle.Primary),
                    new ButtonProperties($"absence-create-vis:{AbsenceVisibility.StaffOnly}", Msg.Absence.StaffButton(Lang), EmojiProperties.Standard("🙂"), ButtonStyle.Primary),
                ]),
            ],
            title: Msg.Absence.CreateTitle(Lang));

    // Read as a plain string, not bound as an AbsenceVisibility parameter directly —
    // component-interaction enum-from-custom-id binding isn't verified, same caution
    // already applied to Raid's location/RoE's branch.
    [ComponentInteraction("absence-create-vis")]
    public Task<InteractionCallbackProperties<MessageOptions>> NotificationsPrompt(string visibility) =>
        EphemeralEmbedModifyAsync(
            Msg.Absence.NotificationsPrompt(Lang),
            [
                new ActionRowProperties(
                [
                    new ButtonProperties($"absence-create-notify:{visibility}:true", Msg.Absence.NotificationsOffButton(Lang), EmojiProperties.Standard("🔔"), ButtonStyle.Secondary),
                    new ButtonProperties($"absence-create-notify:{visibility}:false", Msg.Absence.NotificationsOnButton(Lang), EmojiProperties.Standard("🔔"), ButtonStyle.Primary),
                ]),
            ]);

    // Must stay a Modal response — Discord has no way to show a modal via ModifyMessage —
    // but since it was opened from a component, submitting it can still ModifyMessage the
    // originating message (see AbsenceModalModule.CreateAbsence).
    [ComponentInteraction("absence-create-notify")]
    public InteractionCallbackProperties<ModalProperties> CreateModal(string visibility, bool suppressNotifications) =>
        InteractionCallback.Modal(new ModalProperties($"absence-create-modal:{visibility}:{suppressNotifications}", Msg.Absence.CreateTitle(Lang),
        [
            new LabelProperties(Msg.Absence.StartDateLabel(Lang), new TextInputProperties("start-date", TextInputStyle.Short) { Placeholder = Msg.Absence.DatePlaceholder(Lang), Required = true }),
            new LabelProperties(Msg.Absence.StartTimeLabel(Lang), new TextInputProperties("start-time", TextInputStyle.Short) { Placeholder = Msg.Absence.TimePlaceholder(Lang), Required = true }),
            new LabelProperties(Msg.Absence.EndDateLabel(Lang), new TextInputProperties("end-date", TextInputStyle.Short) { Placeholder = Msg.Absence.DatePlaceholder(Lang), Required = true }),
            new LabelProperties(Msg.Absence.EndTimeLabel(Lang), new TextInputProperties("end-time", TextInputStyle.Short) { Placeholder = Msg.Absence.TimePlaceholder(Lang), Required = true }),
            new LabelProperties(Msg.Absence.ReasonLabel(Lang), new TextInputProperties("reason", TextInputStyle.Short) { Placeholder = Msg.Absence.OptionalPlaceholder(Lang), Required = false }),
        ]));

    // Same loading-then-edit pattern as ManageAbsences, just via ModifyMessage instead of a
    // brand-new message — this is itself a follow-up step within the wizard, not the entry
    // point, so both the loading state and the final response edit the same message.
    [ComponentInteraction("absence-edit")]
    public Task EditPrompt() =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var own = await absenceService.GetOwnUpcomingAsync(Context.Guild!.Id, Context.User.Id);
            var finalEmbed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, Msg.Absence.EditPickPrompt(Lang, CommanderName.Of(Context.User)),
                title: Msg.Absence.EditTitle(Lang));

            return m =>
            {
                m.Embeds = [finalEmbed];
                m.Components = [new StringMenuProperties("absence-edit-target", own.Select(AbsenceService.BuildOption))];
            };
        });

    [ComponentInteraction("absence-delete")]
    public Task DeletePrompt() =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var own = await absenceService.GetOwnUpcomingAsync(Context.Guild!.Id, Context.User.Id);
            var finalEmbed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, Msg.Absence.DeletePickPrompt(Lang, CommanderName.Of(Context.User)),
                title: Msg.Absence.DeleteTitle(Lang));

            return m =>
            {
                m.Embeds = [finalEmbed];
                m.Components = [new StringMenuProperties("absence-delete-target", own.Select(AbsenceService.BuildOption))];
            };
        });

    [ComponentInteraction("absence-confirm")]
    public Task Confirm(int draftId) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var result = await absenceService.ConfirmDraftAsync(draftId, Context.User.Id);
            return await embedBranding.BrandedEditAsync(Context.Guild!.Id, result);
        });

    [ComponentInteraction("absence-cancel")]
    public Task Cancel(int draftId) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var result = await absenceService.CancelDraftAsync(draftId, Context.User.Id);
            return await embedBranding.BrandedEditAsync(Context.Guild!.Id, result);
        });
}
