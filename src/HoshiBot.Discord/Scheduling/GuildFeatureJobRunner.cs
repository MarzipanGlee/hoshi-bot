using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Logging;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Shared preamble for per-guild feature jobs: fetch the enabled-guild set, optionally re-check that
// each guild still qualifies (an admin can toggle the feature off mid-sweep), and isolate per-guild
// failures so one broken guild can't abort the sweep for everyone after it.
public static class GuildFeatureJobRunner
{
    // recheckAudience: the audience to re-verify per guild (guild-scoped, alliance null). Jobs whose
    // body does its own finer-grained gating (per-alliance links, settings flags, the guild-wide
    // IsEnabledAsync overload) pass null — the runner then runs the body for every fetched guild.
    // The catch keeps each job's own logger category; the job name comes from the concrete type.
    public static async Task ForEachEnabledGuildAsync(
        this IJob job,
        GuildFeatureService featureService,
        GuildFeature feature,
        GuildAudience? recheckAudience,
        ILogger logger,
        Func<ulong, Task> body,
        CancellationToken cancellationToken = default)
    {
        var guildIds = await featureService.GetEnabledGuildIdsAsync(feature, cancellationToken);

        foreach (var guildId in guildIds)
        {
            if (recheckAudience is { } audience &&
                !await featureService.IsEnabledAsync(guildId, feature, audience, null))
                continue;

            try
            {
                await body(guildId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "{Job} failed for guild {GuildId}", job.GetType().Name, guildId);
            }
        }
    }
}
