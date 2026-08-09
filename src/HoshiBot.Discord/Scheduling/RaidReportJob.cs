using HoshiBot.Discord.Alerts;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Hourly sweep for the weekly raid report. The job itself has no schedule knowledge — each alliance
// is due at its own local Monday time, which RaidReportService checks against the alliance's
// timezone, so this only has to tick often enough to catch every whole hour.
//
// [DisallowConcurrentExecution] because a slow guild must not let the next tick start a second pass
// over the same alliances: the per-week marker is written after the post, so overlapping runs could
// both decide the same report is still due.
[DisallowConcurrentExecution]
public class RaidReportJob(RaidReportService reportService) : IJob
{
    public Task Execute(IJobExecutionContext context) =>
        reportService.SendDueReportsAsync(context.CancellationToken);
}
