using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.CommandBridge;

// Staff bridge "Beta-Tests verwalten": self-service toggle of the caller's own beta-tester
// role. Not gated by a GuildFeature (it only ever affects the clicking user's own role).
public class CommandBridgeStaffBetaModule(BetaTesterService betaTesterService, EmbedBranding embedBranding, LanguageResolver languageResolver)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    // Everything here is ephemeral to the clicking staff member — their language.
    private Task<Language> ActingUserLanguageAsync() =>
        languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

    [ComponentInteraction("staff-beta-tests")]
    public async Task<InteractionMessageProperties> Prompt()
    {
        var lang = await ActingUserLanguageAsync();
        var (configured, hasRole) = await betaTesterService.GetStatusAsync(Context.Guild!.Id, Context.User.Id);
        if (!configured)
            return EphemeralReply.Of(Msg.Bridge.BetaRoleNotConfigured(lang));

        var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id,
            Msg.Bridge.BetaStatus(lang, hasRole ? Msg.Bridge.BetaOn(lang) : Msg.Bridge.BetaOff(lang)),
            title: Msg.Bridge.BetaTitle(lang));

        return new InteractionMessageProperties
        {
            Embeds = [embed],
            Flags = MessageFlags.Ephemeral,
            Components =
            [
                new ActionRowProperties(
                [
                    new ButtonProperties("staff-beta-tests-set:on", Msg.Bridge.BetaEnableButton(lang), EmojiProperties.Standard(Icons.Start), ButtonStyle.Primary) { Disabled = hasRole },
                    new ButtonProperties("staff-beta-tests-set:off", Msg.Bridge.BetaDisableButton(lang), EmojiProperties.Standard(Icons.Stop), ButtonStyle.Secondary) { Disabled = !hasRole },
                ]),
            ],
        };
    }

    // Buttons live on this module's own ephemeral message — ModifyMessage is safe.
    [ComponentInteraction("staff-beta-tests-set")]
    public Task Set(string action) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var lang = await ActingUserLanguageAsync();
            var result = await betaTesterService.SetAsync(Context.Guild!.Id, Context.User.Id, action == "on", lang);
            var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, result, title: Msg.Bridge.BetaTitle(lang));
            return m => { m.Embeds = [embed]; m.Components = []; };
        });
}
