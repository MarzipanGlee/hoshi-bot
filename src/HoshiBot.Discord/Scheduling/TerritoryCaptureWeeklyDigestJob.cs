using Quartz;

namespace HoshiBot.Discord.Scheduling;

public class TerritoryCaptureWeeklyDigestJob(TerritoryCaptureDigestService digestService) : IJob
{
    public Task Execute(IJobExecutionContext context) => digestService.SendWeeklyDigestsAsync();
}
