using HoshiBot.Domain;

namespace HoshiBot.Domain.Tests;

// Anchored on the two real reports the legacy bot posted (weeks 21 and 22 of 2026), so the port
// can be checked against something that actually shipped rather than against my own arithmetic.
public class RaidReportSchedulerTests
{
    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute = 0) =>
        new(new DateTime(year, month, day, hour, minute, 0), TimeSpan.FromHours(2));

    [Fact]
    public void ReportWeek_OnMonday_CoversThePreviousMondayToSunday()
    {
        // Posted Monday 25 May 2026; the screenshot's Zeitraum reads 18 May to 24 May.
        var (start, end) = RaidReportScheduler.GetReportWeek(Local(2026, 5, 25, 9));

        Assert.Equal(new DateOnly(2026, 5, 18), start);
        Assert.Equal(new DateOnly(2026, 5, 24), end);
    }

    [Fact]
    public void WeekNumber_MatchesTheTitleLegacyPosted()
    {
        // "Raid Bericht Woche 21" for the week starting 18 May, "Woche 22" for 25 May.
        Assert.Equal(21, RaidReportScheduler.WeekNumber(new DateOnly(2026, 5, 18)));
        Assert.Equal(22, RaidReportScheduler.WeekNumber(new DateOnly(2026, 5, 25)));
    }

    [Fact]
    public void ReportWeek_LateOnMonday_StillCoversTheSameWeek()
    {
        // Catch-up after downtime must not roll the window forward, or a week goes unreported.
        var (start, _) = RaidReportScheduler.GetReportWeek(Local(2026, 5, 25, 23, 59));
        Assert.Equal(new DateOnly(2026, 5, 18), start);
    }

    [Theory]
    [InlineData(8, false)]   // before the fire time
    [InlineData(9, true)]    // exactly on it
    [InlineData(23, true)]   // later the same day, so a missed hour still posts
    public void IsDue_OnMonday_FromTheFireTimeOnward(int hour, bool expected)
    {
        var due = RaidReportScheduler.IsDue(Local(2026, 5, 25, hour), new TimeOnly(9, 0));
        Assert.Equal(expected, due);
    }

    [Fact]
    public void IsDue_IsFalseOnEveryOtherDay()
    {
        // Tuesday through Sunday of the same week.
        for (var day = 26; day <= 31; day++)
            Assert.False(RaidReportScheduler.IsDue(Local(2026, 5, day, 12), new TimeOnly(9, 0)));
    }

    [Theory]
    [InlineData(0, 0, 45, "45s")]
    [InlineData(0, 20, 55, "20m55s")]
    [InlineData(2, 20, 55, "2h20m55s")]      // the duration in the week-21 report
    [InlineData(26, 0, 1, "26h0m1s")]        // hours are not wrapped into days, as in Go
    public void FormatDuration_MatchesGoDurationString(int hours, int minutes, int seconds, string expected) =>
        Assert.Equal(expected, RaidReportScheduler.FormatDuration(new TimeSpan(hours, minutes, seconds)));

    [Fact]
    public void FormatDuration_DropsFractionalSeconds()
    {
        // Legacy stripped these with a regex; here the TimeSpan components simply truncate.
        var duration = new TimeSpan(0, 0, 2, 20, 750);
        Assert.Equal("2m20s", RaidReportScheduler.FormatDuration(duration));
    }
}
