using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain;

// Pure capture-window computation for Territory Capture zones — no Discord/EF refs, same
// spirit as DurationParser. Tier/duration is a small fixed lookup (not per-zone data);
// Weekday/CaptureTimeUtc are per-zone and nullable (unknown until observed/entered).
public static class TerritoryCaptureScheduler
{
    public const DayOfWeek TcWeekStartWeekday = DayOfWeek.Wednesday;

    // 1->30, 2->45, 3->60 ported from territories-common.yag's $tierDuration. 4->90 is an
    // unconfirmed placeholder — Qoda is the first Tier 4 zone seen and its real duration
    // isn't known yet; update once confirmed.
    private static readonly Dictionary<int, int> TierDurationMinutes = new()
    {
        [1] = 30,
        [2] = 45,
        [3] = 60,
        [4] = 90,
    };

    // The Wednesday that starts the TC week containing the given instant (UTC calendar
    // days — Zurich-local precision is explicitly out of scope).
    public static DateOnly GetWeekStart(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var offset = ((int)today.DayOfWeek - (int)TcWeekStartWeekday + 7) % 7;
        return today.AddDays(-offset);
    }

    public static (DateTimeOffset Start, DateTimeOffset End)? GetCaptureWindow(StfcTerritory territory, DateOnly weekStart)
    {
        if (territory.Weekday is not { } weekday || territory.CaptureTimeUtc is not { } captureTime)
            return null;

        var dayOffset = ((int)weekday - (int)TcWeekStartWeekday + 7) % 7;
        var captureDate = weekStart.AddDays(dayOffset);
        var start = new DateTimeOffset(captureDate, captureTime, TimeSpan.Zero);
        var durationMinutes = TierDurationMinutes.GetValueOrDefault(territory.Tier, 30);

        return (start, start.AddMinutes(durationMinutes));
    }
}
