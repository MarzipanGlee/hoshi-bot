using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord;

// Every real (non-ephemeral, non-preview) bot message shares this exact author/footer
// style, ported directly from legacy's own fixed embed template
// (Commands/static_data/definitions-common.yag:44-49): author = the bot's own live
// per-guild nickname (so renaming the bot in a guild is reflected automatically) + a
// fixed icon/link; footer = the guild's icon + "{GuildName} - Hoshi Bot by MarzipanGlee".
public class EmbedBranding(GatewayClient gatewayClient, EmbedBrandingOptions options)
{
    private const string DeveloperName = "MarzipanGlee";
    private const string AuthorUrl = "https://stfc.bot";

    // Matches legacy's exact palette (Commands/static_data/definitions-common.yag:22-35),
    // reused here instead of each call site guessing its own color. Bot is legacy's
    // default for general/informational embeds (dark blue); Danger is what active
    // raid/shield alarms use (crimson) — not the same red as an outright error/failure.
    public static readonly Color BotColor = new(0x00008B);
    public static readonly Color DangerColor = new(0xDC143C);
    public static readonly Color InformationColor = new(0xFFFAF0);
    public static readonly Color WarningColor = new(0xFFD700);

    public async Task<EmbedAuthorProperties> BuildAuthorAsync(ulong guildId)
    {
        return new EmbedAuthorProperties
        {
            Name = await GetBotDisplayNameAsync(guildId),
            IconUrl = $"{options.PublicWebBaseUrl}/images/officers/{ResolveOfficerIconFileName(guildId)}",
            Url = AuthorUrl,
        };
    }

    // The bot's live display name in a guild — its per-guild nickname if set, else the global
    // name/username. Shared by the embed author line and the AI-chat name trigger (which reacts
    // when a message addresses the bot by this name), so both see the same name a member does.
    public async Task<string> GetBotDisplayNameAsync(ulong guildId)
    {
        string? nickname = null;

        if (gatewayClient.Cache.Guilds.TryGetValue(guildId, out var cachedGuild)
            && cachedGuild.Users.TryGetValue(gatewayClient.Id, out var cachedBotMember))
        {
            nickname = cachedBotMember.Nickname;
        }
        else
        {
            // Discord always includes the bot's own member object in GUILD_CREATE
            // regardless of intents, so this cache miss should be rare — REST fallback
            // kept simple, same Forbidden/NotFound-safe pattern used everywhere else
            // isn't needed here since a failure just means falling back to the global name.
            try
            {
                var botMember = await gatewayClient.Rest.GetGuildUserAsync(guildId, gatewayClient.Id);
                nickname = botMember.Nickname;
            }
            catch (RestException)
            {
                // Fall through to the global-name fallback below.
            }
        }

        var currentUser = gatewayClient.Cache.User;
        return nickname ?? currentUser?.GlobalName ?? currentUser?.Username ?? "Hoshi Bot";
    }

    public EmbedFooterProperties BuildFooter(ulong guildId)
    {
        var guild = gatewayClient.Cache.Guilds.GetValueOrDefault(guildId);
        var text = guild is not null ? $"{guild.Name} - Hoshi Bot by {DeveloperName}" : $"Hoshi Bot by {DeveloperName}";

        return new EmbedFooterProperties
        {
            Text = text,
            IconUrl = guild?.HasIcon == true ? guild.GetIconUrl()?.ToString() : null,
        };
    }

    // Hardcoded to one portrait for now — legacy never mapped this to the nickname
    // either. A likely future paid per-guild customization point: every call site goes
    // through BuildAuthorAsync rather than a hardcoded filename directly, so only this
    // method's body needs to change later (e.g. reading a GuildSettings column) instead
    // of every embed-building call site.
    private static string ResolveOfficerIconFileName(ulong guildId) => "sato.png";
}
