using System.Globalization;

namespace HoshiBot.Domain;

// When the weekly raid report fires and which week it covers — pure date arithmetic, no Discord/EF,
// same shape as TerritoryCaptureScheduler.
//
// Ported from hoshi-bot-yagpdb's raid-report.yag, which ran on an hourly trigger restricted to
// Monday 07:00 UTC — 09:00 Europe/Zurich in summer, 08:00 in winter, because YAGPDB could only
// exclude UTC hours. Here the fire time is alliance-local and DST-aware, so "Monday 09:00" stays
// 09:00 all year.
public static class RaidReportScheduler
{
    // Monday, because the report is about the week that just ended. Not configurable: a "weekly
    // report" whose week boundary moved would make two consecutive reports overlap or skip days.
    public const DayOfWeek ReportWeekday = DayOfWeek.Monday;

    public const string DefaultLocalTime = "09:00";

    // True for every tick at or after the local time on a Monday. Deliberately not an equality
    // check: the caller's per-week dedup makes it fire once, and this way a report still goes out
    // after downtime that spanned the exact hour rather than being silently skipped.
    public static bool IsDue(DateTimeOffset nowInZone, TimeOnly localTime) =>
        nowInZone.DayOfWeek == ReportWeekday && TimeOnly.FromDateTime(nowInZone.DateTime) >= localTime;

    // The week the report covers: the Monday..Sunday that ENDED before the current one began.
    // Posting on Monday 25 May reports 18 May 00:00:00 through 24 May 23:59:59.999…, which is what
    // the legacy report showed under "Zeitraum".
    //
    // End is exclusive-adjacent rather than a bare date so a raid at 23:59:59.7 on Sunday belongs to
    // the week it happened in; callers compare with < End.
    public static (DateOnly Start, DateOnly EndInclusive) GetReportWeek(DateTimeOffset nowInZone)
    {
        var today = DateOnly.FromDateTime(nowInZone.DateTime);
        var daysSinceMonday = ((int)today.DayOfWeek - (int)ReportWeekday + 7) % 7;
        var start = today.AddDays(-daysSinceMonday - 7);
        return (start, start.AddDays(6));
    }

    // ISO 8601 week number, which is what the legacy title showed: the week starting Monday
    // 18 May 2026 is "Woche 21". .NET's ISOWeek matches YAGPDB's weekNumber for Monday-anchored
    // weeks, and unlike Calendar.GetWeekOfYear it needs no culture to agree.
    public static int WeekNumber(DateOnly weekStart) =>
        ISOWeek.GetWeekOfYear(weekStart.ToDateTime(TimeOnly.MinValue));

    // A raid shorter than this is not worth reporting — a misclick, or a raid that ended the moment
    // it was called. Legacy dropped them with the same threshold, so a week's count means the same
    // thing before and after the port.
    public static readonly TimeSpan MinimumReportableRaid = TimeSpan.FromMinutes(5);

    // Go's Duration.String() as the legacy report printed it: "2h20m55s", dropping leading units
    // that are zero and the fractional seconds legacy stripped with a regex. Always shows seconds,
    // so a raid under a minute reads "45s" rather than empty.
    public static string FormatDuration(TimeSpan duration)
    {
        var total = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        var hours = (int)total.TotalHours;

        return hours > 0
            ? $"{hours}h{total.Minutes}m{total.Seconds}s"
            : total.Minutes > 0
                ? $"{total.Minutes}m{total.Seconds}s"
                : $"{total.Seconds}s";
    }
}
