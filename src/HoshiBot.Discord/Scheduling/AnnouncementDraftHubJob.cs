using HoshiBot.Discord.Announcements;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Keeps the draft channel's standing hub message present and current.
//
// A job rather than a Publish button in the admin (which is how the Command Bridge hub works): that
// hub's buttons change whenever a feature is toggled, so it genuinely needs republishing on demand,
// whereas this one is fixed text and a legend built from the severity list. Sweeping it means an
// admin who configures a draft channel gets the hub without knowing to press anything, and one who
// deletes the message gets it back.
//
// The interval is generous because there is nothing time-sensitive here — the sweep exists to
// converge, not to react.
[DisallowConcurrentExecution]
public class AnnouncementDraftHubJob(AnnouncementDraftHubService hubService) : IJob
{
    public Task Execute(IJobExecutionContext context) =>
        hubService.RefreshAllAsync(context.CancellationToken);
}
