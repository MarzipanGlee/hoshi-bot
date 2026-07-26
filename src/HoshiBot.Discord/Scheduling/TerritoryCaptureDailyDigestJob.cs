using HoshiBot.Discord.TerritoryCapture;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Obsolete: replaced by TerritoryCaptureDigestSweepJob (per-alliance configurable times). Retained only
// so the persistent Quartz store's cleanup (Program.cs DeleteJob) can never hit a missing JobClass type;
// delete once every environment's store is confirmed clean.
[Obsolete("Replaced by TerritoryCaptureDigestSweepJob; kept for safe persistent-store cleanup.")]
public class TerritoryCaptureDailyDigestJob(TerritoryCaptureDigestService digestService) : IJob
{
    public Task Execute(IJobExecutionContext context) => digestService.SendDailyDigestsAsync();
}
