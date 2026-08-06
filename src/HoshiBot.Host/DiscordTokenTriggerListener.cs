using HoshiBot.Discord.Permissions;
using HoshiBot.Domain.Entities;
using Quartz;
using Quartz.Listener;

namespace HoshiBot.Host;

// Stops every scheduled job from starting once the Discord token is known-invalid.
//
// NetCord's gateway gives up permanently on close code 4004 (authentication failed), but nothing
// stops the REST client — so a rotated or revoked token leaves ~20 jobs firing calls that all 401,
// each one counting toward Discord's 10,000-invalid-requests-per-10-minutes ban threshold. The bot is
// already dead at that point; the only question is whether it takes the IP down with it.
//
// A listener rather than a check inside each job: one place, and jobs never start rather than each
// discovering the failure mid-run. It is a TRIGGER listener because that is where Quartz puts
// VetoJobExecution — IJobListener only observes jobs that are already going to run.
//
// Nothing resets the flag: recovering means a new token, which means a restart.
public sealed class DiscordTokenTriggerListener(DiscordApiHealth health, ILogger<DiscordTokenTriggerListener> logger) : TriggerListenerSupport
{
    public override string Name => nameof(DiscordTokenTriggerListener);

    public override Task<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (!health.TokenInvalid)
            return Task.FromResult(false);

        logger.LogWarning("Skipping {Job}: the Discord token is invalid, so every request it makes would be rejected.",
            context.JobDetail.Key.Name);
        return Task.FromResult(true);
    }
}
