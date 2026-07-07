using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord.Announcements;

// MessageCommandContext is a sibling of ApplicationCommandContext (both just implement
// IApplicationCommandContext), not a subtype — needs its own module base, confirmed via
// reflection against the installed NetCord package before writing this.
public class AnnouncementMessageCommandModule(GuildFeatureService featureService) : ApplicationCommandModule<MessageCommandContext>
{
    [MessageCommand("Vorschau erstellen")]
    public async Task<InteractionMessageProperties> Preview()
    {
        if (!await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.Announcements))
            return EphemeralReply.Of(GuildFeatureService.DisabledMessage(GuildFeature.Announcements));

        var draft = Context.Target;
        var (title, body) = AnnouncementService.ParseDraft(draft.Content);

        var embed = new EmbedProperties
        {
            Title = string.IsNullOrWhiteSpace(title) ? "*(kein Titel)*" : title,
            Description = string.IsNullOrWhiteSpace(body) ? "*(kein Text)*" : body,
            Footer = draft.Attachments.Count > 0
                ? new EmbedFooterProperties { Text = $"{draft.Attachments.Count} Anhang/Anhänge" }
                : null,
        };

        var idPart = $"{draft.ChannelId}:{draft.Id}";
        return new InteractionMessageProperties
        {
            Content = "Vorschau — wähle die Alarmstufe zum Veröffentlichen, oder brich ab.",
            Embeds = [embed],
            Flags = MessageFlags.Ephemeral,
            Components =
            [
                new ActionRowProperties(
                [
                    new ButtonProperties($"announcement-publish-normal:{idPart}", "Normal", EmojiProperties.Standard("🟩"), ButtonStyle.Success),
                    new ButtonProperties($"announcement-publish-elevated:{idPart}", "Erhöht", EmojiProperties.Standard("🟨"), ButtonStyle.Primary),
                    new ButtonProperties($"announcement-publish-high:{idPart}", "Hoch", EmojiProperties.Standard("🟥"), ButtonStyle.Danger),
                    new ButtonProperties($"announcement-publish-direct:{idPart}", "Direkt", EmojiProperties.Standard("🟦"), ButtonStyle.Primary),
                    new ButtonProperties("announcement-cancel", "Abbrechen", ButtonStyle.Secondary),
                ]),
            ],
        };
    }
}
