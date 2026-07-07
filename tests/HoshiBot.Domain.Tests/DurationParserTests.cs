using HoshiBot.Domain;
using Xunit;

namespace HoshiBot.Domain.Tests;

public class DurationParserTests
{
    [Theory]
    [InlineData("2d3h45m", 2, 3, 45)]
    [InlineData("2d 3h 45m", 2, 3, 45)]
    [InlineData("2D 3H 45M", 2, 3, 45)]
    public void Parse_CompoundDuration_ReturnsExpectedTimeSpan(string input, int days, int hours, int minutes)
    {
        var result = DurationParser.Parse(input);

        Assert.Equal(new TimeSpan(days, hours, minutes, 0), result);
    }

    [Fact]
    public void Parse_MinutesOnly_ReturnsExpectedTimeSpan()
    {
        var result = DurationParser.Parse("90m");

        Assert.Equal(TimeSpan.FromMinutes(90), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage")]
    [InlineData("2x3h")]
    [InlineData("0m")]
    public void Parse_InvalidInput_ReturnsNull(string input)
    {
        var result = DurationParser.Parse(input);

        Assert.Null(result);
    }
}
