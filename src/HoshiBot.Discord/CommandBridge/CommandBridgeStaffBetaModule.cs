using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.CommandBridge;

// Staff bridge "Beta-Tests verwalten": self-service toggle of the caller's own beta-tester
// role. Not gated by a GuildFeature (it only ever affects the clicking user's own role).
public class CommandBridgeStaffBetaModule(BetaTesterService betaTesterService, EmbedBranding embedBranding)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("staff-beta-tests")]
    public async Task<InteractionMessageProperties> Prompt()
    {
        var (configured, hasRole) = await betaTesterService.GetStatusAsync(Context.Guild!.Id, Context.User.Id);
        if (!configured)
            return EphemeralReply.Of("Es ist keine Beta-Tester-Rolle konfiguriert (siehe Guild-Einstellungen).");

        var embed = await BuildEmbedAsync(
            $"Die Beta-Tests sind für Dich aktuell:\n\n- **{(hasRole ? "EIN" : "AUS")}**",
            "Beta-Tests verwalten");

        return new InteractionMessageProperties
        {
            Embeds = [embed],
            Flags = MessageFlags.Ephemeral,
            Components =
            [
                new ActionRowProperties(
                [
                    new ButtonProperties("staff-beta-tests-set:on", "Beta-Tests einschalten", EmojiProperties.Standard("▶️"), ButtonStyle.Primary) { Disabled = hasRole },
                    new ButtonProperties("staff-beta-tests-set:off", "Beta-Tests ausschalten", EmojiProperties.Standard("⏹️"), ButtonStyle.Secondary) { Disabled = !hasRole },
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
            var embed = await BuildEmbedAsync(result, "Beta-Tests verwalten");
            return m => { m.Embeds = [embed]; m.Components = []; };
        });

    private async Task<EmbedProperties> BuildEmbedAsync(string description, string title) => new()
    {
        Title = title,
        Description = description,
        Color = EmbedBranding.BotColor,
        Author = await embedBranding.BuildAuthorAsync(Context.Guild!.Id),
        Footer = embedBranding.BuildFooter(Context.Guild!.Id),
    };
}
