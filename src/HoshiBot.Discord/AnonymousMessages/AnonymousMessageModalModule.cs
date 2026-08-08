using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.AnonymousMessages;

public class AnonymousMessageModalModule(AnonymousMessageService anonymousMessageService, GuildAllianceService allianceService, EmbedBranding embedBranding,
    LanguageResolver languageResolver) : ComponentInteractionModule<ModalInteractionContext>
{
    // Always opened from CommandBridgeButtonModule.ContactSeniorStaffPrompt's ephemeral
    // wizard message, so ModifyMessage is safe here — never the public hub.
    [ComponentInteraction("anonymous-message-modal")]
    public Task SendAnonymousMessage(string audience) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            // The status edit is ephemeral to the sender — their language, resolved here and
            // passed down so the anonymous relay never has to see who is sending.
            var callerLanguage = await languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

            var values = Context.Components
                .OfType<Label>()
                .Select(l => l.Component)
                .OfType<TextInput>()
                .ToDictionary(i => i.CustomId, i => i.Value);

            var subject = values.GetValueOrDefault("subject") ?? "";
            var message = values.GetValueOrDefault("message") ?? "";

            var (parsedAudience, guildAllianceId, _) = await allianceService.ResolveScopeAsync(Context.Guild!.Id, audience);
            var result = await anonymousMessageService.SendAsync(Context.Guild!.Id, parsedAudience, guildAllianceId, subject, message, callerLanguage);
            return await embedBranding.BrandedEditAsync(Context.Guild!.Id, result);
        });
}
