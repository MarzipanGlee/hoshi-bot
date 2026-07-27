using HoshiBot.Data;
using NetCord;
using NetCord.Hosting.Gateway;

namespace HoshiBot.Host;

// Records each interacting user's Discord client locale (Interaction.UserLocale) into
// DiscordUser.DiscordLocale, so DMs and jobs — which have no interaction in hand — can
// still resolve the user's automatic language (LanguagePolicy.ForUser's stored-locale
// leg). Runs alongside the normal interaction pipeline; failures only cost the locale
// update, never the interaction itself.
public class UserLocaleSyncHandler(IServiceScopeFactory scopeFactory, ILogger<UserLocaleSyncHandler> logger) : IInteractionCreateGatewayHandler
{
    public async ValueTask HandleAsync(Interaction interaction)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<LanguageResolver>();
            await resolver.RecordUserLocaleAsync(interaction.User.Id, interaction.UserLocale);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record user locale for {UserId}", interaction.User.Id);
        }
    }
}
