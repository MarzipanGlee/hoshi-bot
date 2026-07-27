using HoshiBot.Discord.TerritoryCapture;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Half-hourly sweep that fires each alliance's weekly/daily Territory Capture digest when its configured
// local time is due in the alliance's timezone — replaces the two fixed-time Europe/Zurich crons
// (TerritoryCaptureWeeklyDigestJob/TerritoryCaptureDailyDigestJob). See TerritoryCaptureDigestService.
//
// DisallowConcurrentExecution: due-ness stays true for every tick until the digest's own dedup row is
// committed (see TerritoryCaptureScheduler's "due every tick" comment), so an overlapping run could
// otherwise pass the dedup check before the first run's insert lands and double-send/double-ping —
// the same race class already guarded against on the sibling TerritoryCaptureReminderJob.
[DisallowConcurrentExecution]
public class TerritoryCaptureDigestSweepJob(TerritoryCaptureDigestService digestService) : IJob
{
    public Task Execute(IJobExecutionContext context) => digestService.RunDigestSweepAsync(DateTimeOffset.UtcNow);
}
