using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Sweeps abandoned modal-retry drafts (an error was shown, but the user never clicked
// Zurück or Abbrechen) — same TTL spirit as AbsenceReportRefreshJob's stale-draft sweep.
public class PendingModalInputSweepJob(PendingModalInputService pendingModalInputService) : IJob
{
    public Task Execute(IJobExecutionContext context) => pendingModalInputService.SweepStaleAsync();
}
