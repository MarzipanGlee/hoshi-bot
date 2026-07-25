using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Half-hourly sweep that fires each alliance's weekly/daily Territory Capture digest when its configured
// local time is due in the alliance's timezone — replaces the two fixed-time Europe/Zurich crons
// (TerritoryCaptureWeeklyDigestJob/TerritoryCaptureDailyDigestJob). See TerritoryCaptureDigestService.
public class TerritoryCaptureDigestSweepJob(TerritoryCaptureDigestService digestService) : IJob
{
    public Task Execute(IJobExecutionContext context) => digestService.RunDigestSweepAsync(DateTimeOffset.UtcNow);
}
