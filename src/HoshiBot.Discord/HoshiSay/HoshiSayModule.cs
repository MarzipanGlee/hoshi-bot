using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord.HoshiSay;

// Admin "/hoshi-say": an admin describes what Hoshi should convey and picks a target channel; Hoshi
// composes the message in her own voice (reusing the guild's AI backend, via
// AiChatService.ComposeMessageAsync) and posts it there as a plain chat message — no embed, so it
// reads like a natural Hoshi chat line rather than a bot announcement.
//
// Access model: gated to the guild's configured admin channel (GuildSettings.AdminChannelId) — the
// command only actually posts when invoked from there, so who-can-reach-the-admin-channel *is* the
// permission gate (no separate role/permission check, by design). The ephemeral ack/confirmation
// shown to the invoking admin is a branded embed (repo convention); only the message Hoshi posts to
// the target channel is plain text.
//
// Requires the AI backend (GuildFeature.AiBackend) to be configured — ComposeMessageAsync returns
// null otherwise, and we report that instead of posting an empty message.
public class HoshiSayModule(HoshiBotDbContext db, AiChatService aiChat, GatewayClient gatewayClient, EmbedBranding embedBranding)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("hoshi-say", "Lass Hoshi eine Nachricht in einen Kanal schreiben (nur im Admin-Kanal nutzbar)",
        Contexts = [InteractionContextType.Guild])]
    public Task Say(
        [SlashCommandParameter(Name = "kanal", Description = "Zielkanal, in den Hoshi die Nachricht schreibt")]
        TextGuildChannel channel,
        [SlashCommandParameter(Name = "auftrag", Description = "Was Hoshi vermitteln soll, z. B. tröste Speed, er hat seine Nodes verloren")]
        string instruction)
        => Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var guildId = Context.Guild!.Id;

            // Admin-channel gate: this command only works when invoked from the guild's configured
            // admin channel (channel access is the permission model). No admin channel set ⇒ unusable.
            var settings = await db.GuildSettings.FindAsync(guildId);
            if (settings?.AdminChannelId is not { } adminChannelId)
                return "⚠️ Für diese Gilde ist kein Admin-Kanal konfiguriert. Bitte im Web-Admin einen Admin-Kanal festlegen – dieser Befehl funktioniert nur dort.";
            if (Context.Channel.Id != adminChannelId)
                return $"⚠️ Dieser Befehl funktioniert nur im Admin-Kanal (<#{adminChannelId}>).";

            var text = await aiChat.ComposeMessageAsync(guildId, instruction, CancellationToken.None);
            if (text is null)
                return "⚠️ Ich konnte gerade keine Nachricht verfassen – ist das KI-Backend (AiBackend) für diese Gilde konfiguriert? Versuch es sonst gleich noch einmal.";

            // Post as a plain chat message (no embed) so it reads like a natural Hoshi line. Never let a
            // composed line ping @everyone, roles, or members — the persona prompt also forbids <@…>.
            await gatewayClient.Rest.SendMessageAsync(channel.Id, new MessageProperties
            {
                Content = text,
                AllowedMentions = AllowedMentionsProperties.None,
            });

            return $"✅ In <#{channel.Id}> gepostet:\n\n{text}";
        });
}
