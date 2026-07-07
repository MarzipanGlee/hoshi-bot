using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using Xunit;

namespace HoshiBot.Domain.Tests;

public class TerritoryCaptureSchedulerTests
{
    [Fact]
    public void GetWeekStart_ReturnsPrecedingWednesday()
    {
        // 2026-07-06 is a Monday.
        var now = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

        var weekStart = TerritoryCaptureScheduler.GetWeekStart(now);

        Assert.Equal(new DateOnly(2026, 7, 1), weekStart);
        Assert.Equal(DayOfWeek.Wednesday, weekStart.DayOfWeek);
    }

    [Fact]
    public void GetCaptureWindow_KnownZone_ReturnsExpectedWindow()
    {
        // Qoda: Tier 4, Monday 19:00 UTC.
        var qoda = new StfcTerritory { Name = "Qoda", Tier = 4, Weekday = DayOfWeek.Monday, CaptureTimeUtc = new TimeOnly(19, 0) };
        var weekStart = new DateOnly(2026, 7, 1); // Wednesday.

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
}
