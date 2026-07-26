using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Shared diff-and-notify scaffold for the alert-channel notify jobs (ServerStatus,
// InfiniteIncursions, AllianceTournament): load the rows whose observed state no longer
// matches what was last announced, resolve the guilds each row concerns, post one branded
// embed per guild via the audience-gated dispatcher path, then write the Notified* marks
// back in a single SaveChangesAsync. StfcClientReleaseNotifyJob deliberately does NOT use
// this base — it fetches external HTTP sources per platform, inserts a baseline row on
// first detection (hence its [DisallowConcurrentExecution]), and dispatches to
// GuildFeatureChannel ids with a per-platform role instead of GuildAlertChannel rows.
public abstract class DiffNotifyJobBase<TRow>(
    HoshiBotDbContext db, NotificationDispatcher dispatcher, EmbedBranding embedBranding) : IJob
{
    // Exposed for the subclasses' own diff/guild-resolution queries.
    protected HoshiBotDbContext Db { get; } = db;

    protected abstract GuildAlertChannelKind ChannelKind { get; }
    protected abstract GuildFeature Feature { get; }

    // Embed title shared by every announcement a job sends; null (the default) omits it.
    protected virtual string? Title => null;

    // Only the rows that actually need announcing — the per-job Notified*-vs-observed diff
    // (plus any job-specific filters, e.g. "event start still in the future") lives here.
    protected abstract Task<List<TRow>> LoadPendingRowsAsync();

    // The guilds a given row concerns (e.g. via their GuildServer tracking rows).
    protected abstract Task<List<ulong>> ResolveGuildIdsAsync(TRow row);

    protected abstract (string Content, NetCord.Color Color) BuildAnnouncement(TRow row);

    // Copy the observed state into the row's Notified* column(s); the base persists it.
    protected abstract void MarkNotified(TRow row);

    public async Task Execute(IJobExecutionContext context)
    {
        foreach (var row in await LoadPendingRowsAsync())
        {
            var guildIds = await ResolveGuildIdsAsync(row);
            var (content, color) = BuildAnnouncement(row);

            foreach (var guildId in guildIds)
            {
                var embed = await embedBranding.BuildBrandedAsync(guildId, content, color, Title);
                await dispatcher.SendPublicToEnabledAudiencesAsync(
                    guildId, ChannelKind, Feature, content, embed: embed);
            }

            MarkNotified(row);
        }

        await Db.SaveChangesAsync();
    }
}
