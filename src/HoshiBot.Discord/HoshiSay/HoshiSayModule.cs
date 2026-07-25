using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Domain.Entities;
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
// Access model: this is the GuildFeature.HoshiSay feature, so it's (1) gated on the feature being
// enabled for the guild, and (2) runnable only from one of the feature's configured trigger channels
// (a GuildFeatureChannel list, set in the Web editor) — who can reach those channels *is* the
// permission gate (no separate role/permission check, by design). The ephemeral ack/confirmation
// shown to the invoking admin is a branded embed (repo convention); only the message Hoshi posts to
// the target channel is plain text. Depends on AiBackend (the model that composes the text).
public class HoshiSayModule(
    AiChatService aiChat,
    GuildFeatureService featureService,
    GuildFeatureChannelService channelService,
    GatewayClient gatewayClient,
    EmbedBranding embedBranding)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("hoshi-say", "Lass Hoshi eine Nachricht in einen Kanal schreiben (nur in den konfigurierten Kanälen)",
        Contexts = [InteractionContextType.Guild])]
    public Task Say(
        [SlashCommandParameter(Name = "kanal", Description = "Zielkanal, in den Hoshi die Nachricht schreibt")]
        TextGuildChannel channel,
        [SlashCommandParameter(Name = "auftrag", Description = "Was Hoshi vermitteln soll, z. B. tröste Speed, er hat seine Nodes verloren")]
        string instruction,
        [SlashCommandParameter(Name = "mitglied", Description = "Optional: Mitglied, das erwähnt/gepingt werden soll (auch wenn es keinen Admin-Zugriff hat)")]
        User? mitglied = null)
        => Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var guildId = Context.Guild!.Id;

            // Feature gate (Discord can send stale interactions from an unrefreshed hub/command list).
            if (await featureService.EnsureEnabledAsync(guildId, GuildFeature.HoshiSay) is { } disabled)
                return disabled;

            // Trigger-channel gate: only runnable from a configured Hoshi-Say channel (channel access
            // is the permission model). No trigger channel set ⇒ unusable until one is configured.
            var triggerChannels = await channelService.GetChannelsAsync(guildId, GuildFeature.HoshiSay, GuildAudience.Guild);
            if (triggerChannels.Count == 0)
                return "⚠️ Es ist kein Trigger-Kanal für „Hoshi sag“ konfiguriert. Bitte im Web-Admin unter „Hoshi Say“ mindestens einen Kanal festlegen.";
            if (!triggerChannels.Contains(Context.Channel.Id))
                return $"⚠️ Dieser Befehl funktioniert nur in den dafür konfigurierten Kanälen: {string.Join(", ", triggerChannels.Select(c => $"<#{c}>"))}.";

            var text = await aiChat.ComposeMessageAsync(
                guildId, instruction, mitglied?.Id, mitglied is null ? null : CommanderName.Of(mitglied), CancellationToken.None);
            if (text is null)
                return "⚠️ Ich konnte gerade keine Nachricht verfassen – ist das KI-Backend (AiBackend) für diese Gilde konfiguriert? Versuch es sonst gleich noch einmal.";

            // Post as a plain chat message (no embed) so it reads like a natural Hoshi line. Only the
            // explicitly-picked member may be pinged; @everyone/roles and any other stray <@…> stay inert.
            await gatewayClient.Rest.SendMessageAsync(channel.Id, new MessageProperties
            {
                Content = text,
                AllowedMentions = mitglied is { } m
                    ? new AllowedMentionsProperties { Everyone = false, ReplyMention = false, AllowedRoles = [], AllowedUsers = [m.Id] }
                    : AllowedMentionsProperties.None,
            });

            return $"✅ In <#{channel.Id}> gepostet:\n\n{text}";
        });
}
