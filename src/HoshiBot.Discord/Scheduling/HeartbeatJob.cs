using Microsoft.Extensions.Logging;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Template job proving the Quartz.NET scheduling pipeline works end-to-end.
// Real jobs (thread cleanup, shield/raid warnings, TC reminders, ...) follow this
// same shape: implement IJob, resolve a Domain service via constructor injection,
// and register a trigger for it in HoshiBot.Host's Program.cs.
public class HeartbeatJob(ILogger<HeartbeatJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Hoshi Bot scheduling is online at {Time}", DateTimeOffset.Now);
        return Task.CompletedTask;
    }
}
