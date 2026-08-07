using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
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
    EmbedBranding embedBranding,
    GuildMemberNames memberNames,
    LanguageResolver languageResolver)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    // Canonical metadata is English (like every other command); the German names/descriptions —
    // including the original "auftrag"/"mitglied" option names — live on as localizations in
    // HoshiBot.Host/Localizations/de.json.
    [SlashCommand("hoshi-say", "Let Hoshi post a message in this channel (requires the authorized role)",
        Contexts = [InteractionContextType.Guild])]
    public Task Say(
        [SlashCommandParameter(Name = "task", Description = "What Hoshi should convey, e.g. comfort Speed, he lost his nodes")]
        string instruction,
        [SlashCommandParameter(Name = "member", Description = "Optional: member to mention/ping")]
        User? member = null)
        => Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var guildId = Context.Guild!.Id;

            // Every catalog string here — gates, failure, the Posted confirmation quoting the
            // composed text — is the ephemeral reply to the invoking admin, so it all follows
            // the acting user's language. The message Hoshi posts publicly is the LLM-composed
            // text itself, which never goes through the catalog.
            var lang = await languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, guildId);

            // Feature gate (Discord can send stale interactions from an unrefreshed command list).
            if (await featureService.EnsureEnabledAsync(guildId, GuildFeature.HoshiSay, lang) is { } disabled)
                return disabled;

            // Role gate: only members holding the configured allowed role may run the command.
            //
            // The pattern variable is `actor`, NOT `member`: C# lets a local declared inside this
            // lambda shadow the enclosing method's parameter of the same name, with no warning, and
            // naming it `member` here silently rebound every later use — the composed text and the
            // ping both went to whoever ran the command instead of the member they picked.
            var allowedRole = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.HoshiSay, GuildAudience.Guild, null, HoshiSaySettingKeys.AllowedRole);
            if (allowedRole is not { } roleId)
                return Msg.Say.NoRoleConfigured(lang);
            if (Context.User is not GuildUser actor || !actor.RoleIds.Contains(roleId))
                return Msg.Say.RoleRequired(lang, $"<@&{roleId}>");

            var text = await aiChat.ComposeMessageAsync(
                guildId, Context.Channel.Id, instruction, member?.Id, member is null ? null : await memberNames.ResolveAsync(guildId, member), CancellationToken.None);
            if (text is null)
                return Msg.Say.ComposeFailed(lang);

            // Post into the channel the command was used in, as a plain chat message (no embed) so it
            // reads like a natural Hoshi line. Only the explicitly-picked member may be pinged;
            // @everyone/roles and any other stray <@…> stay inert.
            await gatewayClient.Rest.SendMessageAsync(Context.Channel.Id, new MessageProperties
            {
                Content = text,
                AllowedMentions = member is { } m
                    ? new AllowedMentionsProperties { Everyone = false, ReplyMention = false, AllowedRoles = [], AllowedUsers = [m.Id] }
                    : AllowedMentionsProperties.None,
            });

            return Msg.Say.Posted(lang, text);
        });
}
