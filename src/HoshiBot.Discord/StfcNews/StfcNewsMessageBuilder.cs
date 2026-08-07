using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;

namespace HoshiBot.Discord.StfcNews;

// The single place that renders an StfcNewsPost's current state into an embed/button set —
// reused by StfcNewsNotifyJob's initial send, StfcNewsService's submit/confirm/resolve
// handlers, and the periodic StfcNewsStatsRefreshJob, so every guild's copy of the message
// always reflects the exact same shared state. The post goes to each guild's AdminChannelId
// (guild-scoped), so callers pass that guild's language and build once per distinct language.
public static class StfcNewsMessageBuilder
{
    public static (EmbedProperties Embed, IReadOnlyList<ButtonProperties>? Buttons) Build(StfcNewsPost post, int confirmationCount, Language lang)
    {
        if (post.ResolvedAt is not null)
        {
            return (new EmbedProperties
            {
                Description = Msg.News.ResolvedBody(lang, post.Title, post.Link, post.SubmittedDate!.Value),
                Color = EmbedBranding.InformationColor,
            }, null);
        }

        if (post.SubmittedDate is { } date)
        {
            return (new EmbedProperties
            {
                Description = Msg.News.SuggestedBody(lang, post.Title, post.Link,
                    $"<@{post.SubmittedByDiscordUserId}>", date, confirmationCount, post.RequiredConfirmations),
                Color = EmbedBranding.WarningColor,
            }, [
                new ButtonProperties($"stfc-news-confirm:{post.Id}", Msg.News.ConfirmButton(lang), EmojiProperties.Standard(Icons.Ok), ButtonStyle.Success),
                new ButtonProperties($"stfc-news-edit:{post.Id}", Msg.News.EditButton(lang), EmojiProperties.Standard(Icons.Edit), ButtonStyle.Secondary),
            ]);
        }

        return (new EmbedProperties
        {
            Description = Msg.News.NewPostBody(lang, post.Title, post.Link),
            Color = EmbedBranding.InformationColor,
        }, [
            new ButtonProperties($"stfc-news-enter-date:{post.Id}", Msg.News.EnterDateTitle(lang), EmojiProperties.Standard(Icons.Date), ButtonStyle.Primary),
        ]);
    }
}
