using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain.Tests;

public class StfcServerTests
{
    [Fact]
    public void RegionServer_JoinsRegionAndNumberWithAHyphen() =>
        Assert.Equal("EU-164", StfcServer.RegionServer("EU", 164));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegionServer_WithoutARegion_IsJustTheNumber(string? regionName)
    {
        // A caller that did not Include the region used to render "-164": a separator with nothing
        // in front of it. RegionServerPicker is that caller — it filters by region, so it never
        // loaded one.
        Assert.Equal("164", StfcServer.RegionServer(regionName, 164));
    }

    [Fact]
    public void DisplayName_FallsBackToTheNumberWhenTheRegionIsNotLoaded()
    {
        var server = new StfcServer { Id = 164, Name = "Mindmeld" };
        Assert.Equal("164 Mindmeld", server.DisplayName);
    }
}
