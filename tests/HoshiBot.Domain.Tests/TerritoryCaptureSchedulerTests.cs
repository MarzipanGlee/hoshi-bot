using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using Xunit;

namespace HoshiBot.Domain.Tests;

public class TerritoryCaptureSchedulerTests
{
    [Fact]
    public void GetWeekStart_ReturnsPrecedingFriday()
    {
        // 2026-07-06 is a Monday.
        var now = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

        var weekStart = TerritoryCaptureScheduler.GetWeekStart(now);

        Assert.Equal(new DateOnly(2026, 7, 3), weekStart);
        Assert.Equal(DayOfWeek.Friday, weekStart.DayOfWeek);
    }

    [Fact]
    public void GetCaptureWindow_KnownZone_ReturnsExpectedWindow()
    {
        // Qoda: Tier 4, Monday 19:00 UTC.
        var qoda = new StfcTerritory { Name = "Qoda", Tier = 4, Weekday = DayOfWeek.Monday, CaptureTimeUtc = new TimeOnly(19, 0) };
        var weekStart = new DateOnly(2026, 7, 3); // Friday.

        var window = TerritoryCaptureScheduler.GetCaptureWindow(qoda, weekStart);

        Assert.NotNull(window);
        Assert.Equal(new DateTimeOffset(2026, 7, 6, 19, 0, 0, TimeSpan.Zero), window.Value.Start);
        Assert.Equal(new DateTimeOffset(2026, 7, 6, 20, 30, 0, TimeSpan.Zero), window.Value.End);
    }

    [Fact]
    public void GetCaptureWindow_UnknownSchedule_ReturnsNull()
    {
        var zone = new StfcTerritory { Name = "Zhian", Tier = 2, Weekday = null, CaptureTimeUtc = null };

        var window = TerritoryCaptureScheduler.GetCaptureWindow(zone, new DateOnly(2026, 7, 1));

        Assert.Null(window);
    }

    [Fact]
    public void WeeklyDigestWeekday_IsThursdayForFridayAnchor()
    {
        Assert.Equal(DayOfWeek.Thursday, TerritoryCaptureScheduler.WeeklyDigestWeekday);
    }

    [Fact]
    public void IsWeeklyDigestDue_OnlyOnWeekdayAtOrAfterTime()
    {
        var time = new TimeOnly(9, 0);
        var thursday = new DateOnly(2026, 7, 2); // Thursday
        var friday = new DateOnly(2026, 7, 3);

        Assert.False(TerritoryCaptureScheduler.IsWeeklyDigestDue(AtLocal(thursday, 8, 30), time)); // before time
        Assert.True(TerritoryCaptureScheduler.IsWeeklyDigestDue(AtLocal(thursday, 9, 0), time));   // exactly at time
        Assert.True(TerritoryCaptureScheduler.IsWeeklyDigestDue(AtLocal(thursday, 12, 0), time));  // after time
        Assert.False(TerritoryCaptureScheduler.IsWeeklyDigestDue(AtLocal(friday, 9, 0), time));    // wrong day
    }

    [Fact]
    public void IsDailyDigestDue_AtOrAfterTimeAnyDay()
    {
        var time = new TimeOnly(19, 0);
        var day = new DateOnly(2026, 7, 8);

        Assert.False(TerritoryCaptureScheduler.IsDailyDigestDue(AtLocal(day, 18, 30), time));
        Assert.True(TerritoryCaptureScheduler.IsDailyDigestDue(AtLocal(day, 19, 0), time));
        Assert.True(TerritoryCaptureScheduler.IsDailyDigestDue(AtLocal(day, 23, 30), time));
    }

    [Theory]
    [InlineData(2026, 7, 2, 7)]  // CEST (+2): 09:00 Europe/Zurich == 07:00 UTC (Thursday)
    [InlineData(2027, 1, 7, 8)]  // CET  (+1): 09:00 Europe/Zurich == 08:00 UTC (Thursday)
    public void IsWeeklyDigestDue_ResolvesLocalTimeAcrossDst(int year, int month, int day, int expectedUtcHour)
    {
        var zurich = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");
        var time = new TimeOnly(9, 0);
        var dueUtc = new DateTimeOffset(year, month, day, expectedUtcHour, 0, 0, TimeSpan.Zero);

        // The tick 30 min before the resolved instant is not yet due; the resolved instant is.
        Assert.False(TerritoryCaptureScheduler.IsWeeklyDigestDue(TimeZoneInfo.ConvertTime(dueUtc.AddMinutes(-30), zurich), time));
        Assert.True(TerritoryCaptureScheduler.IsWeeklyDigestDue(TimeZoneInfo.ConvertTime(dueUtc, zurich), time));
    }

    // The predicates read only DayOfWeek + local time-of-day, so the fixed offset here is irrelevant.
    private static DateTimeOffset AtLocal(DateOnly date, int hour, int minute) =>
        new(date.Year, date.Month, date.Day, hour, minute, 0, TimeSpan.FromHours(2));
}
