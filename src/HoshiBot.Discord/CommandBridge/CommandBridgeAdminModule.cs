using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord.CommandBridge;

// Which bridge(s) /post-command-bridge should (re)post. Separate from the CommandBridgeKind enum
// so it can carry the "All" choice.
public enum CommandBridgeChoice
{
    All,
    User,
    Staff,
    Friends,
}

// Posts/refreshes the "Kommandobrücke" hub messages — persistent messages with buttons that
// are the real entry point for members/staff (matching the legacy bot's UX) rather than slash
// commands. Re-run after adding buttons or toggling a feature so a hub reflects the current
// set. The actual building lives in CommandBridgeHubService (shared with the Web-triggered
// republish job).
public class CommandBridgeAdminModule(CommandBridgeHubService hubService, GuildAllianceService allianceService, EmbedBranding embedBranding,
    LanguageResolver languageResolver)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("post-command-bridge", "Post or refresh the Command Bridge hub message(s)",
        DefaultGuildPermissions = Permissions.ManageGuild, Contexts = [InteractionContextType.Guild])]
    public Task PostCommandBridge(CommandBridgeChoice bridge = CommandBridgeChoice.All) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var guildId = Context.Guild!.Id;

            // The result lines are an ephemeral reply to the invoking admin — their language;
            // the hub messages themselves render in each alliance's own language (hubService).
            var lang = await languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, guildId);

            var bridges = bridge switch
            {
                CommandBridgeChoice.User => [CommandBridgeKind.User],
                CommandBridgeChoice.Staff => [CommandBridgeKind.Staff],
                CommandBridgeChoice.Friends => [CommandBridgeKind.Friends],
                _ => new[] { CommandBridgeKind.User, CommandBridgeKind.Staff, CommandBridgeKind.Friends },
            };

            // Command Bridges are per-alliance — (re)post each linked alliance's hub(s).
            var links = await allianceService.GetLinksAsync(guildId);
            if (links.Count == 0)
                return Msg.Bridge.NoAllianceLinked(lang);

            var lines = new List<string>();
            foreach (var link in links)
            {
                var tag = link.StfcAlliance.Tag;
                foreach (var target in bridges)
                {
                    var result = await hubService.PublishAsync(guildId, link.Id, target);
                    lines.Add(result switch
                    {
                        CommandBridgePublishResult.Updated => Msg.Bridge.HubUpdated(lang, tag, Label(target, lang)),
                        CommandBridgePublishResult.Posted => Msg.Bridge.HubPosted(lang, tag, Label(target, lang)),
                        _ => Msg.Bridge.HubNoChannel(lang, tag, Label(target, lang)),
                    });
                }
            }

            return string.Join('\n', lines);
        });

    private static string Label(CommandBridgeKind bridge, Language lang) => bridge switch
    {
        CommandBridgeKind.Staff => Msg.Bridge.KindStaff(lang),
        CommandBridgeKind.Friends => Msg.Bridge.KindFriends(lang),
        _ => Msg.Bridge.KindUser(lang),
    };
}
