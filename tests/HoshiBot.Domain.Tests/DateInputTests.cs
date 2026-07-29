using HoshiBot.Domain.Localization;

namespace HoshiBot.Domain.Tests;

public class DateInputTests
{
    // The edit-modal prefill (FormatDate/FormatTime) must parse back to the same value
    // under the same language — otherwise submitting an untouched edit modal would fail.
    [Theory]
    [InlineData(Language.De)]
    [InlineData(Language.En)]
    public void Prefill_round_trips_through_parsing(Language language)
    {
        var value = new DateTimeOffset(2026, 8, 1, 14, 30, 0, TimeSpan.Zero);

        Assert.True(DateInput.TryParseDate(DateInput.FormatDate(value, language), language, out var date));
        Assert.True(DateInput.TryParseTime(DateInput.FormatTime(value), language, out var time));

        Assert.Equal(new DateOnly(2026, 8, 1), date);
        Assert.Equal(new TimeOnly(14, 30), time);
    }

    [Fact]
    public void Prefill_uses_each_languages_convention()
    {
        var value = new DateTimeOffset(2026, 8, 1, 14, 30, 0, TimeSpan.Zero);

        Assert.Equal("01.08.2026", DateInput.FormatDate(value, Language.De));
        Assert.Equal("2026-08-01", DateInput.FormatDate(value, Language.En));
        Assert.Equal("14:30", DateInput.FormatTime(value));
    }

    // Pre-localization habit: everyone typed German dd.MM.yyyy — it must keep parsing
    // with German day/month order under EVERY resolved language (en-US free-form parsing
    // would have read 01.08. as January 8).
    [Theory]
    [InlineData(Language.De)]
    [InlineData(Language.En)]
    public void German_date_input_parses_under_any_language(Language language)
    {
        Assert.True(DateInput.TryParseDate("01.08.2026", language, out var date));
        Assert.Equal(new DateOnly(2026, 8, 1), date);
    }

    [Theory]
    [InlineData(Language.De)]
    [InlineData(Language.En)]
    public void Iso_date_input_parses_under_any_language(Language language)
    {
        Assert.True(DateInput.TryParseDate("2026-08-01", language, out var date));
        Assert.Equal(new DateOnly(2026, 8, 1), date);
    }

    [Fact]
    public void Culture_short_date_parses_for_english()
    {
        Assert.True(DateInput.TryParseDate("8/1/2026", Language.En, out var date));
        Assert.Equal(new DateOnly(2026, 8, 1), date);
    }

    [Theory]
    [InlineData(Language.De)]
    [InlineData(Language.En)]
    public void TwentyFourHour_time_parses_under_any_language(Language language)
    {
        Assert.True(DateInput.TryParseTime("14:30", language, out var time));
        Assert.Equal(new TimeOnly(14, 30), time);
    }

    [Fact]
    public void Culture_short_time_parses_for_english()
    {
        Assert.True(DateInput.TryParseTime("2:30 PM", Language.En, out var time));
        Assert.Equal(new TimeOnly(14, 30), time);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("01.08.")]
    [InlineData("tomorrow")]
    public void Invalid_date_input_is_rejected(string? text)
    {
        Assert.False(DateInput.TryParseDate(text, Language.De, out _));
        Assert.False(DateInput.TryParseDate(text, Language.En, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("25:70")]
    [InlineData("noonish")]
    public void Invalid_time_input_is_rejected(string? text)
    {
        Assert.False(DateInput.TryParseTime(text, Language.De, out _));
        Assert.False(DateInput.TryParseTime(text, Language.En, out _));
    }
}
