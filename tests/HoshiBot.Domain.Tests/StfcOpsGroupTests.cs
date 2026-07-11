using HoshiBot.Domain.Entities;
using Xunit;

namespace HoshiBot.Domain.Tests;

public class StfcOpsGroupTests
{
    [Theory]
    [InlineData(1, StfcOpsGroup.G1)]
    [InlineData(9, StfcOpsGroup.G1)]
    [InlineData(10, StfcOpsGroup.G2)]
    [InlineData(19, StfcOpsGroup.G2)]
    [InlineData(20, StfcOpsGroup.G3)]
    [InlineData(39, StfcOpsGroup.G3)]
    [InlineData(40, StfcOpsGroup.G4)]
    [InlineData(50, StfcOpsGroup.G4)]
    [InlineData(51, StfcOpsGroup.G5)]
    [InlineData(60, StfcOpsGroup.G5)]
    [InlineData(61, StfcOpsGroup.G6)]
    [InlineData(70, StfcOpsGroup.G6)]
    [InlineData(71, StfcOpsGroup.G7)]
    [InlineData(80, StfcOpsGroup.G7)]
    public void FromLevel_WithinBucket_ReturnsExpectedGroup(int level, StfcOpsGroup expected)
    {
        Assert.Equal(expected, StfcOpsGroupExtensions.FromLevel(level));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(81)]
    [InlineData(null)]
    public void FromLevel_OutsideDefinedRange_ReturnsNull(int? level)
    {
        Assert.Null(StfcOpsGroupExtensions.FromLevel(level));
    }
}
