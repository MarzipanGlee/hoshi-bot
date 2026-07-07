using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord;

public class PingModule : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("ping", "Replies with pong")]
    public static string Ping() => "Pong!";
}
