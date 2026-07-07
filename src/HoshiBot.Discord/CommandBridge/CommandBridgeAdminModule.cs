using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord.CommandBridge;

// Posts/refreshes the "Kommandobrücke" hub message — a persistent message with buttons
// that's the real entry point for members (matching the legacy bot's actual UX), rather
// than slash commands. Re-run whenever new buttons are added in a later phase, or after
// toggling a feature (via the guild settings page) so the hub reflects the current set.
public class CommandBridgeAdminModule(HoshiBotDbContext db, GatewayClient gatewayClient, EmbedBranding embedBranding, GuildFeatureService featureService)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("post-command-bridge", "Post or refresh the Command Bridge hub message",
        DefaultGuildPermissions = Permissions.ManageGuild, Contexts = [InteractionContextType.Guild])]
    public async Task<InteractionMessageProperties> PostCommandBridge()
    {
        var guildId = Context.Guild!.Id;

        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings?.CommandBridgeChannelId is not { } channelId)
            return EphemeralReply.Of("Set the Command Bridge channel first (via the guild settings page).");

        var embed = new EmbedProperties
        {
            Title = "Kommandobrücke",
            Description = "Commander, ich stehe zu Deinen Diensten! Wähle die gewünschte Aktion mit Hilfe der Schaltflächen.\n\nWie lautet Dein Befehl, Commander?",
            Color = EmbedBranding.BotColor,
            Author = await embedBranding.BuildAuthorAsync(guildId),
            Footer = embedBranding.BuildFooter(guildId),
        };

        // Order/colors/icons matched to the legacy hub: every button is Primary — the
        // "green"/"red" look on some legacy buttons is just the emoji itself, not a
        // Success/Danger-styled button. Ticket/Anonymous Message are reached through a
        // single "Führungsstab kontaktieren" button (see CommandBridgeButtonModule),
        // matching legacy's own two-step flow instead of two direct hub buttons — that
        // button itself only shows if at least one of the two is enabled.
        var disabled = await featureService.GetDisabledAsync(guildId);
        var row1 = new List<ButtonProperties>();
        if (!disabled.Contains(GuildFeature.RoeViolationReports))
            row1.Add(new ButtonProperties("roe-violation-report", "ROE Verstoss melden", EmojiProperties.Standard("🚫"), ButtonStyle.Primary));
        if (!disabled.Contains(GuildFeature.ShieldReminders))
            row1.Add(new ButtonProperties("shield-reminder-setup", "Schilderinnerung einrichten", EmojiProperties.Standard("⏰"), ButtonStyle.Primary));
        if (!disabled.Contains(GuildFeature.RaidAlerts))
            row1.Add(new ButtonProperties("raid-report", "Raid melden", EmojiProperties.Standard("🚨"), ButtonStyle.Primary));

        var row2 = new List<ButtonProperties>();
        if (!disabled.Contains(GuildFeature.Announcements))
            row2.Add(new ButtonProperties("announcement-show-unread", "Ungelesene Ankündigungen", EmojiProperties.Standard("🟩"), ButtonStyle.Primary));
        if (!disabled.Contains(GuildFeature.AlertsOptIn))
            row2.Add(new ButtonProperties("alerts-manage", "Alarme verwalten", EmojiProperties.Standard("🔔"), ButtonStyle.Primary));
        if (!disabled.Contains(GuildFeature.Absences))
            row2.Add(new ButtonProperties("absence-manage", "Abwesenheiten verwalten", EmojiProperties.Standard("⛺"), ButtonStyle.Primary));

        var row3 = new List<ButtonProperties>();
        if (!disabled.Contains(GuildFeature.Tickets) || !disabled.Contains(GuildFeature.AnonymousMessaging))
            row3.Add(new ButtonProperties("contact-command-staff", "Führungsstab kontaktieren", EmojiProperties.Standard("📮"), ButtonStyle.Primary));

        var components = new[] { row1, row2, row3 }
            .Where(row => row.Count > 0)
            .Select(row => (IMessageComponentProperties)new ActionRowProperties(row))
            .ToArray();

        if (settings.CommandBridgeMessageId is { } messageId)
        {
            try
            {
                await gatewayClient.Rest.ModifyMessageAsync(channelId, messageId, m =>
                {
                    m.Embeds = [embed];
                    m.Components = components;
                });
                return EphemeralReply.Of("Command Bridge hub message updated.");
            }
            catch (RestException)
            {
                // Message was deleted or otherwise unreachable — fall through and re-post.
            }
        }

        var message = await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties
        {
            Embeds = [embed],
            Components = components,
        });

        settings.CommandBridgeMessageId = message.Id;
        await db.SaveChangesAsync();

        return EphemeralReply.Of("Command Bridge hub message posted.");
    }
}
