using HoshiBot.Discord.TerritoryCapture;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Posts the per-capture "capture soon" reminder and sweeps away expired TC messages. Inserts a
// TerritoryCaptureSentMessage the first time it sees a capture, so it must not run concurrently:
// two overlapping runs could both see "not yet reminded" and collide on the unique (link, dedup
// key) index (the same first-run-collision gotcha as the other row-creating jobs).
[DisallowConcurrentExecution]
public class TerritoryCaptureReminderJob(TerritoryCaptureDigestService digestService) : IJob
{
    public Task Execute(IJobExecutionContext context) => digestService.SendCaptureRemindersAsync();
}
