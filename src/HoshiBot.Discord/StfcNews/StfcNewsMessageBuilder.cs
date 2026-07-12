using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;

namespace HoshiBot.Discord.StfcNews;

// The single place that renders an StfcNewsPost's current state into an embed/button set —
// reused by StfcNewsNotifyJob's initial send, StfcNewsService's submit/confirm/resolve
// handlers, and the periodic StfcNewsStatsRefreshJob, so every guild's copy of the message
// always reflects the exact same shared state.
public static class StfcNewsMessageBuilder
{
    public static (EmbedProperties Embed, IReadOnlyList<ButtonProperties>? Buttons) Build(StfcNewsPost post, int confirmationCount)
    {
        if (post.ResolvedAt is not null)
        {
            return (new EmbedProperties
            {
                Description = $"📰 **[{post.Title}]({post.Link})**\n\n" +
                    $"✅ Event date confirmed: **{post.SubmittedDate:yyyy-MM-dd}**. Notifications will go out automatically.",
                Color = EmbedBranding.InformationColor,
            }, null);
        }

        if (post.SubmittedDate is { } date)
        {
            return (new EmbedProperties
            {
                Description = $"📰 **[{post.Title}]({post.Link})**\n\n" +
                    $"📅 <@{post.SubmittedByDiscordUserId}> suggested **{date:yyyy-MM-dd}**. " +
                    $"({confirmationCount}/{post.RequiredConfirmations} confirmed). Please confirm if this looks right.",
                Color = EmbedBranding.WarningColor,
            }, [
                new ButtonProperties($"stfc-news-confirm:{post.Id}", "Confirm", EmojiProperties.Standard("✅"), ButtonStyle.Success),
                new ButtonProperties($"stfc-news-edit:{post.Id}", "Edit", EmojiProperties.Standard("✏️"), ButtonStyle.Secondary),
            ]);
        }

        return (new EmbedProperties
        {
            Description = $"📰 **[{post.Title}]({post.Link})**\n\n" +
                "A new post was just published on the official STFC blog. Please read it and enter the event date.",
            Color = EmbedBranding.InformationColor,
        }, [
            new ButtonProperties($"stfc-news-enter-date:{post.Id}", "Enter Event Date", EmojiProperties.Standard("📅"), ButtonStyle.Primary),
        ]);
    }
}
