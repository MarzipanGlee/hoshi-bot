using HoshiBot.Data;
using HoshiBot.Discord.Boarding;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Boarding's safety net and its queue drain.
//
// The gateway handler is the fast path — someone joins, they get boarded. This job exists for the
// cases it cannot cover: a member whose alliance link landed after they joined, a confirm that got
// half-way and failed, a guild the bot was offline for, and the two things the Web admin can ask
// for (publish the message, board the people who were already here).
//
// It is strictly FORWARD-ONLY. It boards members with no BoardingEntry and it finishes entries that
// stalled; it never decides someone needs boarding because of what roles they hold. A member who
// confirmed and later left the alliance loses the member role by another job's hand, and a
// role-driven rule would board them again — and again next week, DM and all. The entry row is the
// memory that stops it.
[DisallowConcurrentExecution]
public class BoardingSyncJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    BoardingService boarding,
    GuildFeatureService featureService,
    PlayerLinkService playerLinks,
    ILogger<BoardingSyncJob> logger) : IJob
{
    // Boarding a member costs a role write and possibly a DM. Capped per run so a guild that just
    // enabled the feature spreads its first pass over several ticks instead of firing hundreds of
    // writes at once — the same pacing MemberOnboardingSyncJob uses for the same reason.
    private const int MaxPerRun = 20;

    private const int MaxAttempts = 5;

    public async Task Execute(IJobExecutionContext context)
    {
        await DrainRequestsAsync(context.CancellationToken);

        await this.ForEachEnabledGuildAsync(featureService, GuildFeature.Boarding, recheckAudience: null, logger,
            guildId => SyncGuildAsync(guildId, context.CancellationToken), context.CancellationToken);
    }

    // ---- The Web admin's two buttons -------------------------------------------------------

    private async Task DrainRequestsAsync(CancellationToken cancellationToken)
    {
        var requests = await db.BoardingRequests.OrderBy(r => r.RequestedAt).ToListAsync(cancellationToken);
        if (requests.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;

        foreach (var group in requests.GroupBy(r => (r.GuildId, r.Audience, r.GuildAllianceId, r.Kind)))
        {
            if (!ShouldAttempt(group, now))
                continue;

            var scope = new BoardingScopes.Scope(group.Key.Audience, group.Key.GuildAllianceId);

            try
            {
                var done = group.Key.Kind switch
                {
                    BoardingRequestKind.Publish => await boarding.RefreshMessageAsync(group.Key.GuildId, scope.Audience, scope.GuildAllianceId, cancellationToken),
                    _ => await BackfillAsync(group.Key.GuildId, scope, cancellationToken),
                };

                if (!done)
                {
                    // Not an exception — the scope simply is not configured enough yet. Say so, so
                    // the admin sees a reason instead of a request that never clears.
                    RecordFailure(group, now, "Not configured: needs a channel, a message and both roles.");
                    continue;
                }
            }
            catch (RestException ex)
            {
                RecordFailure(group, now, $"{(int)ex.StatusCode} {ex.Error?.Message ?? ex.ReasonPhrase}");
                logger.LogWarning(ex, "Boarding {Kind} failed for guild {GuildId} (attempt {Attempt} of {Max})",
                    group.Key.Kind, group.Key.GuildId, group.Max(r => r.AttemptCount), MaxAttempts);
                continue;
            }

            db.BoardingRequests.RemoveRange(group);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // Boards everyone already in the guild who qualifies, ignoring the joined-after cutoff.
    //
    // SILENT: sendDm is false however the welcome text is configured. One DM to a member who just
    // joined answers something they did; several hundred DMs to people who have been here for months
    // is a campaign, and CONTRIBUTING puts those behind their own opt-in feature.
    private async Task<bool> BackfillAsync(ulong guildId, BoardingScopes.Scope scope, CancellationToken cancellationToken)
    {
        if (await boarding.FindPostAsync(guildId, scope, cancellationToken) is null)
            return false;

        var roster = await GuildRoster.FetchAsync(gatewayClient, guildId, cancellationToken);
        var enabled = await EnabledScopesAsync(guildId);
        var allianceByMember = await AllianceByMemberAsync(guildId, enabled);
        var boarded = 0;

        foreach (var member in roster.Values.Where(m => !m.IsBot))
        {
            if (boarded >= MaxPerRun)
                break;

            if (BoardingScopes.Claim(enabled, allianceByMember.GetValueOrDefault(member.Id)) != scope)
                continue;

            if (await boarding.BoardAsync(guildId, member, scope, sendDm: false, cancellationToken))
                boarded++;
        }

        logger.LogInformation("Boarding backfill boarded {Count} members in guild {GuildId}", boarded, guildId);

        // Keep the request queued while there is more to do: the next tick continues where this one
        // stopped, and BoardingEntry means it never re-does anyone.
        return boarded < MaxPerRun;
    }

    // ---- The periodic pass ------------------------------------------------------------------

    private async Task SyncGuildAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var enabled = await EnabledScopesAsync(guildId);
        if (enabled.Count == 0)
            return;

        await RetryStalledAsync(guildId, cancellationToken);

        // Everything below is about boarding NEW members, so a scope with no cutoff recorded does
        // nothing — see GuildEnabledFeature.EnabledAt. That is the safe direction: a null cutoff
        // means "we do not know when this was switched on", and boarding an entire guild on a guess
        // is exactly what the Backfill button exists to make deliberate.
        var cutoffs = await CutoffsAsync(guildId);
        if (cutoffs.Count == 0)
            return;

        var roster = await GuildRoster.FetchAsync(gatewayClient, guildId, cancellationToken);
        var allianceByMember = await AllianceByMemberAsync(guildId, enabled);
        var boarded = 0;

        foreach (var member in roster.Values.Where(m => !m.IsBot))
        {
            if (boarded >= MaxPerRun)
                break;

            if (BoardingScopes.Claim(enabled, allianceByMember.GetValueOrDefault(member.Id)) is not { } scope)
                continue;

            // GuildUser.JoinedAt, not GuildMember.JoinedAt: the latter is stamped whenever the bot
            // first sees a user, so a member who has been here since 2024 and clicked a button today
            // would look like a fresh join.
            if (!cutoffs.TryGetValue(scope, out var enabledAt) || member.JoinedAt < enabledAt)
                continue;

            if (await boarding.BoardAsync(guildId, member, scope, sendDm: true, cancellationToken))
                boarded++;
        }
    }

    // Entries that stopped part-way: the role grant failed (usually a hierarchy problem an admin has
    // since fixed), or the boarding role never came off. Both finish by replaying the confirm.
    private async Task RetryStalledAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var stalled = await db.BoardingEntries
            .Include(e => e.ReadablePost)
            .Where(e => e.GuildId == guildId && e.Status == BoardingStatus.RoleGrantFailed)
            .Take(MaxPerRun)
            .ToListAsync(cancellationToken);

        foreach (var entry in stalled)
        {
            if (!gatewayClient.Cache.Guilds.TryGetValue(guildId, out var guild)
                || !guild.Users.TryGetValue(entry.DiscordUserId, out var member))
            {
                continue;
            }

            var lang = entry.ReadablePost.Language;
            await boarding.OnConfirmedAsync(entry.ReadablePost, member, lang);
        }
    }

    // ---- Shared ------------------------------------------------------------------------------

    private async Task<List<BoardingScopes.Scope>> EnabledScopesAsync(ulong guildId) =>
        (await db.GuildEnabledFeatures
            .Where(f => f.GuildId == guildId && f.Feature == GuildFeature.Boarding)
            .Select(f => new { f.Audience, f.GuildAllianceId })
            .ToListAsync())
        .Select(f => new BoardingScopes.Scope(f.Audience, f.GuildAllianceId))
        .ToList();

    private async Task<Dictionary<BoardingScopes.Scope, DateTimeOffset>> CutoffsAsync(ulong guildId) =>
        (await db.GuildEnabledFeatures
            .Where(f => f.GuildId == guildId && f.Feature == GuildFeature.Boarding && f.EnabledAt != null)
            .Select(f => new { f.Audience, f.GuildAllianceId, f.EnabledAt })
            .ToListAsync())
        .ToDictionary(f => new BoardingScopes.Scope(f.Audience, f.GuildAllianceId), f => f.EnabledAt!.Value);

    // Every member's linked alliance, as this guild's GuildAlliance id — built once per pass rather
    // than queried per member, because the roster is the whole guild. Empty when no Alliance scope
    // is enabled: a community-only Discord never touches the player tables at all.
    private async Task<Dictionary<ulong, int>> AllianceByMemberAsync(ulong guildId, List<BoardingScopes.Scope> enabled)
    {
        if (!enabled.Any(s => s.Audience == GuildAudience.Alliance))
            return [];

        // StfcAlliance id -> this guild's link id, which is what a Boarding scope is keyed by.
        var linkByStfcAlliance = await db.GuildAlliances
            .Where(ga => ga.GuildId == guildId)
            .ToDictionaryAsync(ga => ga.StfcAllianceId, ga => ga.Id);

        var players = await playerLinks.GetGuildPrimaryPlayersAsync(guildId);

        return players.Values
            .Where(p => p.AllianceId is { } a && linkByStfcAlliance.ContainsKey(a))
            .ToDictionary(p => p.DiscordUserId, p => linkByStfcAlliance[p.AllianceId!.Value]);
    }

    private static bool ShouldAttempt(IEnumerable<BoardingRequest> group, DateTimeOffset now)
    {
        var live = group.Where(r => r.AttemptCount < MaxAttempts).ToList();
        if (live.Count == 0)
            return false;

        var attempts = live.Min(r => r.AttemptCount);
        if (attempts == 0)
            return true;

        var lastAttempt = live.Max(r => r.LastAttemptAt);
        return lastAttempt is not { } last || now - last >= ChannelCooldown.WaitAfter(attempts);
    }

    private static void RecordFailure(IEnumerable<BoardingRequest> group, DateTimeOffset now, string error)
    {
        foreach (var request in group)
        {
            request.AttemptCount++;
            request.LastAttemptAt = now;
            request.LastError = error.Length > 500 ? error[..500] : error;
        }
    }
}
