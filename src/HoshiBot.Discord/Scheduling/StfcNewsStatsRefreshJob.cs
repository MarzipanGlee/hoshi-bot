using HoshiBot.Data;
using HoshiBot.Discord.StfcNews;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Batched "X confirmed" display catch-up for StfcNewsPost messages — StfcNewsService.
// ConfirmDateAsync deliberately skips editing every OTHER guild's message on each individual
// click (to avoid hammering ModifyMessageAsync under rate limits); this job is what
// eventually shows everyone else the current count. Mirrors AnnouncementCounterRefreshJob's
// "compare cached vs. actual, only edit if changed" shape.
public class StfcNewsStatsRefreshJob(HoshiBotDbContext db, StfcNewsService service) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var posts = await db.StfcNewsPosts
            .Include(p => p.GuildMessages)
            .Where(p => p.ResolvedAt == null)
            .ToListAsync(context.CancellationToken);

        foreach (var post in posts)
        {
            var count = await db.StfcEventDateConfirmations
                .CountAsync(c => c.StfcNewsPostId == post.Id, context.CancellationToken);
            if (count != post.LastDisplayedConfirmationCount)
                await service.RefreshAllGuildMessagesAsync(post);
        }
    }
}
