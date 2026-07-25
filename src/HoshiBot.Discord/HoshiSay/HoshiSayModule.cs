using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord.HoshiSay;

// Admin "/hoshi-say": an admin describes what Hoshi should convey; Hoshi composes the message in her
// own voice (reusing the guild's AI backend, via AiChatService.ComposeMessageAsync) and posts it into
// the channel the command was used in, as a plain chat message — no embed, so it reads like a natural
// Hoshi chat line rather than a bot announcement.
//
// Access model: this is the GuildFeature.HoshiSay feature, so it's (1) gated on the feature being
// enabled, and (2) limited to members holding the configured allowed role (a snowflake setting, set
// in the Web editor) — role membership *is* the permission gate. There's no channel parameter: the
// admin simply stands in the target channel and runs the command. The ephemeral ack/confirmation
// shown to the invoking admin is a branded embed (repo convention); only the message Hoshi posts is
// plain text. Depends on AiBackend (the model that composes the text).
public class HoshiSayModule(
    AiChatService aiChat,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    GatewayClient gatewayClient,
    EmbedBranding embedBranding)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("hoshi-say", "Lass Hoshi eine Nachricht in diesen Kanal schreiben (nur mit der berechtigten Rolle)",
        Contexts = [InteractionContextType.Guild])]
    public Task Say(
        [SlashCommandParameter(Name = "auftrag", Description = "Was Hoshi vermitteln soll, z. B. tröste Speed, er hat seine Nodes verloren")]
        string instruction,
        [SlashCommandParameter(Name = "mitglied", Description = "Optional: Mitglied, das erwähnt/gepingt werden soll")]
        User? mitglied = null)
        => Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var guildId = Context.Guild!.Id;

            // Feature gate (Discord can send stale interactions from an unrefreshed command list).
            if (await featureService.EnsureEnabledAsync(guildId, GuildFeature.HoshiSay) is { } disabled)
                return disabled;

            // Role gate: only members holding the configured allowed role may run the command.
            var allowedRole = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.HoshiSay, GuildAudience.Guild, null, HoshiSaySettingKeys.AllowedRole);
            if (allowedRole is not { } roleId)
                return "⚠️ Für „Hoshi sag“ ist keine berechtigte Rolle konfiguriert. Bitte im Web-Admin unter „Hoshi Say“ eine Rolle festlegen.";
            if (Context.User is not GuildUser member || !member.RoleIds.Contains(roleId))
                return $"⚠️ Nur Mitglieder mit der Rolle <@&{roleId}> dürfen diesen Befehl verwenden.";

            var text = await aiChat.ComposeMessageAsync(
                guildId, instruction, mitglied?.Id, mitglied is null ? null : CommanderName.Of(mitglied), CancellationToken.None);
            if (text is null)
                return "⚠️ Ich konnte gerade keine Nachricht verfassen – ist das KI-Backend (AiBackend) für diese Gilde konfiguriert? Versuch es sonst gleich noch einmal.";

            // Post into the channel the command was used in, as a plain chat message (no embed) so it
            // reads like a natural Hoshi line. Only the explicitly-picked member may be pinged;
            // @everyone/roles and any other stray <@…> stay inert.
            await gatewayClient.Rest.SendMessageAsync(Context.Channel.Id, new MessageProperties
            {
                Content = text,
                AllowedMentions = mitglied is { } m
                    ? new AllowedMentionsProperties { Everyone = false, ReplyMention = false, AllowedRoles = [], AllowedUsers = [m.Id] }
                    : AllowedMentionsProperties.None,
            });

            return $"✅ Hoshi hat hier gepostet:\n\n{text}";
        });
}
