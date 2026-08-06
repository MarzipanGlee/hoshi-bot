using System.Collections.Concurrent;

namespace HoshiBot.Domain.Entities;

// Process-wide state about how Discord is answering us. A singleton, because it has to outlive the
// per-fire Quartz scopes it protects (same reason LanguageCache is one).
//
// Two jobs, both of them about Discord's Invalid Request Limit: 10,000 responses of 401, 403 or 429
// in any 10 minutes gets the IP temporarily banned — the whole bot, every guild.
public sealed class DiscordApiHealth
{
    // Discord's own window and ceiling, so the numbers below mean what its guide means.
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private const int Ceiling = 10_000;

    // Warn well before the cliff: by the time we're at the limit the ban has already happened.
    private const int WarnThreshold = Ceiling / 10;

    private readonly ConcurrentQueue<DateTimeOffset> _invalidRequests = new();
    private int _lastWarnedAt;

    // Set once a 401 comes back from Discord. A revoked or rotated token makes EVERY request invalid,
    // and nothing in NetCord stops that: the gateway gives up on close code 4004, but the REST client
    // keeps firing forever, burning ~10,000 invalid requests in minutes across the scheduled jobs.
    // Discord asks explicitly for "stopping further requests after a token becomes invalid".
    //
    // Deliberately one-way and not persisted: recovering means a new token, which means a restart.
    public bool TokenInvalid { get; private set; }

    public void MarkTokenInvalid() => TokenInvalid = true;

    // Returns the count in the current window after recording, so the caller can log when it climbs.
    public int RecordInvalidRequest(DateTimeOffset now)
    {
        _invalidRequests.Enqueue(now);

        var cutoff = now - Window;
        while (_invalidRequests.TryPeek(out var oldest) && oldest < cutoff)
            _invalidRequests.TryDequeue(out _);

        return _invalidRequests.Count;
    }

    // True the first time a count crosses each 1,000-request step past the warn threshold, so a
    // guild misconfiguration that starts generating volume says so once per step rather than on
    // every single request.
    public bool ShouldWarn(int count)
    {
        if (count < WarnThreshold)
        {
            _lastWarnedAt = 0;
            return false;
        }

        var step = count / 1_000;
        if (step <= _lastWarnedAt)
            return false;

        _lastWarnedAt = step;
        return true;
    }

    public static int InvalidRequestCeiling => Ceiling;
}
