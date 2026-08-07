using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;

namespace HoshiBot.Discord.Announcements;

// The 🟩🟨🟥🟦 palette (legacy's, unchanged) is this feature's vocabulary in three places at once:
// the reactions staff click on a draft to publish it, the severity field on the published post, and
// the bridge's unread list. One map, because the reaction handler's emoji → severity lookup has to
// be the exact inverse of the reactions the bot adds — two hand-written switches would drift.
public static class AnnouncementSeverities
{
    // Publication order, which is also the order the reactions are added in.
    public static readonly IReadOnlyList<AnnouncementSeverity> Ordered =
        [AnnouncementSeverity.Normal, AnnouncementSeverity.Elevated, AnnouncementSeverity.High, AnnouncementSeverity.Direct];

    public static string Emoji(AnnouncementSeverity severity) => severity switch
    {
        AnnouncementSeverity.Elevated => Icons.SeverityElevated,
        AnnouncementSeverity.High => Icons.SeverityHigh,
        AnnouncementSeverity.Direct => Icons.SeverityDirect,
        _ => Icons.SeverityNormal,
    };

    // Null for every other emoji — a member reacting 👍 to a draft must be ignored, not guessed at.
    public static AnnouncementSeverity? FromEmoji(string? emoji) =>
        Ordered.Where(s => Emoji(s) == emoji).Select(s => (AnnouncementSeverity?)s).FirstOrDefault();

    public static string Label(AnnouncementSeverity severity, Language lang) => severity switch
    {
        AnnouncementSeverity.Elevated => Msg.Announce.SeverityElevated(lang),
        AnnouncementSeverity.High => Msg.Announce.SeverityHigh(lang),
        AnnouncementSeverity.Direct => Msg.Announce.SeverityDirect(lang),
        _ => Msg.Announce.SeverityNormal(lang),
    };
}
