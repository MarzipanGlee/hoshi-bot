using Quartz;

namespace HoshiBot.Discord.Scheduling;

public class TerritoryCaptureDailyDigestJob(TerritoryCaptureDigestService digestService) : IJob
{
    public Task Execute(IJobExecutionContext context) => digestService.SendDailyDigestsAsync();
}
