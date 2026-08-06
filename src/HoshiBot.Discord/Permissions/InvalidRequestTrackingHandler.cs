using System.Net;
using Microsoft.Extensions.Logging;
using NetCord.Rest;

namespace HoshiBot.Discord.Permissions;

// Wraps NetCord's own request handler so every Discord response passes through one place.
// RestClientConfiguration.RequestHandler (and RestClientOptions.RequestHandler on the hosting side)
// takes an IRestRequestHandler, which is just SendAsync + AddDefaultHeader — so this needs no changes
// at any call site, which is exactly why it beats sprinkling checks through the jobs.
//
// It exists because NetCord deliberately owns 429 and nothing else: pre-emptive per-route and global
// buckets, Retry-After aware, unbounded automatic retry. It does not track Discord's Invalid Request
// Limit (10,000 × 401/403/429 per 10 minutes → temporary IP ban) and does nothing at all about 401 or
// 403. Both of those gaps are ours to cover.
public sealed class InvalidRequestTrackingHandler(
    IRestRequestHandler inner,
    DiscordApiHealth health,
    ILogger<InvalidRequestTrackingHandler> logger) : IRestRequestHandler
{
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        // Once the token is known-bad every request would be another 401, and Discord counts each one
        // toward the ban. Short-circuit rather than ask again. Returning a synthetic response instead
        // of throwing keeps the failure shaped like every other Discord failure, so the existing
        // RestException catch blocks handle it and nothing needs a new code path.
        if (health.TokenInvalid)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                ReasonPhrase = "Discord token is invalid — requests suppressed by InvalidRequestTrackingHandler",
                RequestMessage = request,
            };
        }

        var response = await inner.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            Record(response.StatusCode, request);

        return response;
    }

    public void AddDefaultHeader(string name, IEnumerable<string> values) => inner.AddDefaultHeader(name, values);

    public void Dispose() => inner.Dispose();

    private void Record(HttpStatusCode status, HttpRequestMessage request)
    {
        if (status is HttpStatusCode.Unauthorized)
        {
            // First 401 wins: log once at the level an operator will actually see, then stop.
            if (!health.TokenInvalid)
            {
                logger.LogCritical(
                    "Discord returned 401 Unauthorized for {Method} {Uri} — the bot token is invalid or revoked. "
                    + "Suppressing all further REST requests (every one would be another invalid request toward "
                    + "Discord's 10,000-per-10-minutes ban threshold). Fix the token and restart.",
                    request.Method, request.RequestUri);
                health.MarkTokenInvalid();
            }

            return;
        }

        var count = health.RecordInvalidRequest(DateTimeOffset.UtcNow);
        if (health.ShouldWarn(count))
        {
            logger.LogWarning(
                "{Count} invalid Discord responses (401/403/429) in the last 10 minutes, against a ban threshold of {Ceiling}. "
                + "Most recent: {Status} for {Method} {Uri}. A sustained count usually means a permission or role-hierarchy "
                + "problem in a guild — check the permission check page.",
                count, DiscordApiHealth.InvalidRequestCeiling, (int)status, request.Method, request.RequestUri);
        }
    }
}
