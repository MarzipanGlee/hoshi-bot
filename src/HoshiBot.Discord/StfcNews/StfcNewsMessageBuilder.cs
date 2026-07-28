using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;

namespace HoshiBot.Discord.StfcNews;

// The single place that renders an StfcNewsPost's current state into an embed/button set —
// reused by StfcNewsNotifyJob's initial send, StfcNewsService's submit/confirm/resolve
// handlers, and the periodic StfcNewsStatsRefreshJob, so every guild's copy of the message
// always reflects the exact same shared state.
public static class StfcNewsMessageBuilder
{
    // All strings come from the message catalog (Msg.News); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    public static (EmbedProperties Embed, IReadOnlyList<ButtonProperties>? Buttons) Build(StfcNewsPost post, int confirmationCount)
    {
        if (post.ResolvedAt is not null)
        {
            return (new EmbedProperties
            {
                Description = Msg.News.ResolvedBody(Lang, post.Title, post.Link, post.SubmittedDate!.Value),
                Color = EmbedBranding.InformationColor,
            }, null);
        }

        if (post.SubmittedDate is { } date)
        {
            return (new EmbedProperties
            {
                Description = Msg.News.SuggestedBody(Lang, post.Title, post.Link,
                    $"<@{post.SubmittedByDiscordUserId}>", date, confirmationCount, post.RequiredConfirmations),
                Color = EmbedBranding.WarningColor,
            }, [
                new ButtonProperties($"stfc-news-confirm:{post.Id}", Msg.News.ConfirmButton(Lang), EmojiProperties.Standard("✅"), ButtonStyle.Success),
                new ButtonProperties($"stfc-news-edit:{post.Id}", Msg.News.EditButton(Lang), EmojiProperties.Standard("✏️"), ButtonStyle.Secondary),
            ]);
        }

        return (new EmbedProperties
        {
            Description = Msg.News.NewPostBody(Lang, post.Title, post.Link),
            Color = EmbedBranding.InformationColor,
        }, [
            new ButtonProperties($"stfc-news-enter-date:{post.Id}", Msg.News.EnterDateTitle(Lang), EmojiProperties.Standard("📅"), ButtonStyle.Primary),
        ]);
    }
}
