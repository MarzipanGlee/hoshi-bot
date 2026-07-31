using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain;

// Pure capture-window computation for Territory Capture zones — no Discord/EF refs, same
// spirit as DurationParser. Tier/duration is a small fixed lookup (not per-zone data);
// Weekday/CaptureTimeUtc are per-zone and nullable (unknown until observed/entered).
public static class TerritoryCaptureScheduler
{
    // The TC week starts Friday (Scopely's current cadence — there is no longer a capture-free
    // day). Every consumer derives its week boundary from this one constant, so the weekly digest,
    // daily digest, role-sync and capture reminders all agree on which week a zone falls in.
    public const DayOfWeek TcWeekStartWeekday = DayOfWeek.Friday;

    // The weekly digest posts the day before the week begins (Thursday, for a Friday anchor).
    public static readonly DayOfWeek WeeklyDigestWeekday = (DayOfWeek)(((int)TcWeekStartWeekday + 6) % 7);

    // Digest-due predicates for the half-hourly sweep. They take the alliance-local instant (the caller
    // converts UtcNow into GuildAlliance.TimeZoneId, DST-aware) and the configured local fire time.
    // "Due" is true for every tick at/after the time on the right local day; the digest's own
    // per-day/week dedup makes it fire exactly once and gives automatic catch-up after downtime.
    public static bool IsWeeklyDigestDue(DateTimeOffset nowInZone, TimeOnly weeklyLocalTime) =>
        nowInZone.DayOfWeek == WeeklyDigestWeekday && TimeOnly.FromDateTime(nowInZone.DateTime) >= weeklyLocalTime;

    public static bool IsDailyDigestDue(DateTimeOffset nowInZone, TimeOnly dailyLocalTime) =>
        TimeOnly.FromDateTime(nowInZone.DateTime) >= dailyLocalTime;

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

    // The anchor weekday (see TcWeekStartWeekday) that starts the TC week containing the given
    // instant (UTC calendar days — Zurich-local precision is explicitly out of scope).
    public static DateOnly GetWeekStart(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var offset = ((int)today.DayOfWeek - (int)TcWeekStartWeekday + 7) % 7;
        return today.AddDays(-offset);
    }

    // The start of the *upcoming* TC week: the anchor weekday on or after today. The weekly digest
    // posts the day before the week begins (Thursday, for a Friday anchor) and previews the week that
    // is about to start — so on a Thursday this returns tomorrow (Fri), and a same-day Friday
    // misfire-replay returns today (Fri), never skipping or repeating a week. Contrast GetWeekStart,
    // which snaps *back* to the current week's anchor.
    public static DateOnly GetUpcomingWeekStart(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var offset = ((int)TcWeekStartWeekday - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(offset);
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
