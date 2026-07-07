using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Absences;

public class AbsenceButtonModule(AbsenceService absenceService, EmbedBranding embedBranding) : ComponentInteractionModule<ButtonInteractionContext>
{
    // Only the true entry point (reached from the persistent, non-ephemeral Command
    // Bridge hub message) posts a brand-new ephemeral message. Every step after that
    // edits that same message via ModifyMessage — see EphemeralEmbedModifyAsync below —
    // for the wizard experience the legacy bot had, instead of stacking a fresh ephemeral
    // message per step.
    private async Task<InteractionMessageProperties> EphemeralEmbedAsync(string description, IReadOnlyList<IMessageComponentProperties>? components = null, string? title = null, Color? color = null)
    {
        var embed = await BuildEmbedAsync(description, title, color);
        return new InteractionMessageProperties
        {
            Embeds = [embed],
            Flags = MessageFlags.Ephemeral,
            Components = components,
        };
    }

    private async Task<InteractionCallbackProperties<MessageOptions>> EphemeralEmbedModifyAsync(string description, IReadOnlyList<IMessageComponentProperties>? components = null, string? title = null)
    {
        var embed = await BuildEmbedAsync(description, title);
        return InteractionCallback.ModifyMessage(m =>
        {
            m.Embeds = [embed];
            m.Components = components ?? [];
        });
    }

    private async Task<EmbedProperties> BuildEmbedAsync(string description, string? title = null, Color? color = null)
    {
        var guildId = Context.Guild!.Id;
        return new EmbedProperties
        {
            Title = title,
            Description = description,
            Color = color ?? EmbedBranding.BotColor,
            Author = await embedBranding.BuildAuthorAsync(guildId),
            Footer = embedBranding.BuildFooter(guildId),
        };
    }

    // Fetching upcoming absences can take a moment under load, so — matching legacy's own
    // two-step "wird gesucht..." placeholder before the query, then editing the response
    // once it's done — this sends the loading state itself and edits it in place, rather
    // than returning a value for the framework to send only once everything is ready.
    [ComponentInteraction("absence-manage")]
    public async Task ManageAbsences()
    {
        var loadingMessage = await EphemeralEmbedAsync("Abwesenheiten werden gesucht...",
            title: "Abwesenheiten verwalten", color: EmbedBranding.InformationColor);
        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(loadingMessage));

        var own = await absenceService.GetOwnUpcomingAsync(Context.Guild!.Id, Context.User.Id);
        var hasOwn = own.Count > 0;

        // Edit/Delete are always shown, just disabled when there's nothing to act on yet —
        // matches the legacy bot's behavior instead of hiding the buttons outright.
        var buttons = new List<ButtonProperties>
        {
            new("absence-create", "Abwesenheit erfassen", EmojiProperties.Standard("➕"), ButtonStyle.Success),
            new ButtonProperties("absence-edit", "Abwesenheit bearbeiten", EmojiProperties.Standard("✏️"), ButtonStyle.Primary) { Disabled = !hasOwn },
            new ButtonProperties("absence-delete", "Abwesenheit löschen", EmojiProperties.Standard("✖️"), ButtonStyle.Danger) { Disabled = !hasOwn },
        };

        var description = $"Commander {CommanderName.Of(Context.User)}, hier sind Deine künftigen Abwesenheiten:\n\n" +
            $"{AbsenceService.BuildOwnListText(own)}\n\n" +
            "Wie lautet Dein Befehl, Commander?";

        var finalEmbed = await BuildEmbedAsync(description, title: "Abwesenheiten verwalten");

        await Context.Interaction.ModifyResponseAsync(m =>
        {
            m.Embeds = [finalEmbed];
            m.Components = [new ActionRowProperties(buttons)];
        });
    }

    [ComponentInteraction("absence-create")]
    public Task<InteractionCallbackProperties<MessageOptions>> CreatePrompt() =>
        EphemeralEmbedModifyAsync(
            $"Commander {CommanderName.Of(Context.User)}, darf ich Deine Abwesenheit der Allianz melden oder soll nur der Führungsstab informiert werden?",
            [
                new ActionRowProperties(
                [
                    new ButtonProperties($"absence-create-vis:{AbsenceVisibility.Public}", "Öffentlich", EmojiProperties.Standard("📢"), ButtonStyle.Primary),
                    new ButtonProperties($"absence-create-vis:{AbsenceVisibility.StaffOnly}", "Führungsstab", EmojiProperties.Standard("🙂"), ButtonStyle.Primary),
                ]),
            ],
            title: "Abwesenheit erfassen");

    // Read as a plain string, not bound as an AbsenceVisibility parameter directly —
    // component-interaction enum-from-custom-id binding isn't verified, same caution
    // already applied to Raid's location/RoE's branch.
    [ComponentInteraction("absence-create-vis")]
    public Task<InteractionCallbackProperties<MessageOptions>> NotificationsPrompt(string visibility) =>
        EphemeralEmbedModifyAsync(
            "Benachrichtigungen während der Abwesenheit?",
            [
                new ActionRowProperties(
                [
                    new ButtonProperties($"absence-create-notify:{visibility}:true", "Aus", EmojiProperties.Standard("🔔"), ButtonStyle.Secondary),
                    new ButtonProperties($"absence-create-notify:{visibility}:false", "Ein", EmojiProperties.Standard("🔔"), ButtonStyle.Primary),
                ]),
            ]);

    // Must stay a Modal response — Discord has no way to show a modal via ModifyMessage —
    // but since it was opened from a component, submitting it can still ModifyMessage the
    // originating message (see AbsenceModalModule.CreateAbsence).
    [ComponentInteraction("absence-create-notify")]
    public InteractionCallbackProperties<ModalProperties> CreateModal(string visibility, bool suppressNotifications) =>
        InteractionCallback.Modal(new ModalProperties($"absence-create-modal:{visibility}:{suppressNotifications}", "Abwesenheit erfassen",
        [
            new LabelProperties("Startdatum", new TextInputProperties("start-date", TextInputStyle.Short) { Placeholder = "TT.MM.JJJJ", Required = true }),
            new LabelProperties("Startzeit", new TextInputProperties("start-time", TextInputStyle.Short) { Placeholder = "HH:MM", Required = true }),
            new LabelProperties("Enddatum", new TextInputProperties("end-date", TextInputStyle.Short) { Placeholder = "TT.MM.JJJJ", Required = true }),
            new LabelProperties("Endzeit", new TextInputProperties("end-time", TextInputStyle.Short) { Placeholder = "HH:MM", Required = true }),
            new LabelProperties("Grund", new TextInputProperties("reason", TextInputStyle.Short) { Placeholder = "Optional", Required = false }),
        ]));

    // Same loading-then-edit pattern as ManageAbsences, just via ModifyMessage instead of a
    // brand-new message — this is itself a follow-up step within the wizard, not the entry
    // point, so both the loading state and the final response edit the same message.
    [ComponentInteraction("absence-edit")]
    public async Task EditPrompt()
    {
        var loadingEmbed = await BuildEmbedAsync("Abwesenheiten werden gesucht...",
            title: "Abwesenheit bearbeiten", color: EmbedBranding.InformationColor);
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(m =>
        {
            m.Embeds = [loadingEmbed];
            m.Components = [];
        }));

        var own = await absenceService.GetOwnUpcomingAsync(Context.Guild!.Id, Context.User.Id);
        var finalEmbed = await BuildEmbedAsync($"Commander {CommanderName.Of(Context.User)}, welche Abwesenheitsmeldung willst Du bearbeiten?",
            title: "Abwesenheit bearbeiten");

        await Context.Interaction.ModifyResponseAsync(m =>
        {
            m.Embeds = [finalEmbed];
            m.Components = [new StringMenuProperties("absence-edit-target", own.Select(AbsenceService.BuildOption))];
        });
    }

    // Same loading-then-edit pattern as EditPrompt.
    [ComponentInteraction("absence-delete")]
    public async Task DeletePrompt()
    {
        var loadingEmbed = await BuildEmbedAsync("Abwesenheiten werden gesucht...",
            title: "Abwesenheit löschen", color: EmbedBranding.InformationColor);
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(m =>
        {
            m.Embeds = [loadingEmbed];
            m.Components = [];
        }));

        var own = await absenceService.GetOwnUpcomingAsync(Context.Guild!.Id, Context.User.Id);
        var finalEmbed = await BuildEmbedAsync($"Commander {CommanderName.Of(Context.User)}, welche Abwesenheitsmeldung willst Du löschen?",
            title: "Abwesenheit löschen");

        await Context.Interaction.ModifyResponseAsync(m =>
        {
            m.Embeds = [finalEmbed];
            m.Components = [new StringMenuProperties("absence-delete-target", own.Select(AbsenceService.BuildOption))];
        });
    }

    [ComponentInteraction("absence-confirm")]
    public async Task<InteractionCallbackProperties<MessageOptions>> Confirm(int draftId)
    {
        var result = await absenceService.ConfirmDraftAsync(draftId, Context.User.Id);
        return InteractionCallback.ModifyMessage(m => { m.Content = result; m.Embeds = []; m.Components = []; });
    }

    [ComponentInteraction("absence-cancel")]
    public async Task<InteractionCallbackProperties<MessageOptions>> Cancel(int draftId)
    {
        var result = await absenceService.CancelDraftAsync(draftId, Context.User.Id);
        return InteractionCallback.ModifyMessage(m => { m.Content = result; m.Embeds = []; m.Components = []; });
    }
}
