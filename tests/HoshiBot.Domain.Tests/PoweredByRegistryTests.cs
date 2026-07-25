using HoshiBot.Domain.Attribution;
using HoshiBot.Domain.Entities;
using Xunit;

namespace HoshiBot.Domain.Tests;

public class PoweredByRegistryTests
{
    [Fact]
    public void For_SingleEntity_ReturnsItsSource()
    {
        Assert.Equal([PoweredBySources.StfcPro], PoweredByRegistry.For(typeof(StfcPlayer)));
        Assert.Equal([PoweredBySources.StfcSpace], PoweredByRegistry.For(typeof(StfcSystem)));
        Assert.Equal([PoweredBySources.TerritoryLol], PoweredByRegistry.For(typeof(StfcTerritoryService)));
    }

    [Fact]
    public void For_MixedEntities_DedupesAndPreservesOrder()
    {
        // Territory Capture reads both a territory.lol entity and a stfc.pro one → two credits, in order.
        var sources = PoweredByRegistry.For(typeof(StfcTerritoryService), typeof(StfcTerritoryOwnership));
        Assert.Equal([PoweredBySources.TerritoryLol, PoweredBySources.StfcPro], sources);
    }

    [Fact]
    public void For_SeveralSameSourceEntities_YieldsOneCredit()
    {
        var sources = PoweredByRegistry.For(typeof(StfcPlayer), typeof(StfcAlliance), typeof(StfcServer));
        Assert.Equal([PoweredBySources.StfcPro], sources);
    }

    [Fact]
    public void For_UnregisteredEntity_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PoweredByRegistry.For(typeof(GuildAlliance)));
    }

    [Fact]
    public void AllRegisteredEntities_Resolve()
    {
        foreach (var entity in PoweredByRegistry.RegisteredEntities)
            Assert.Single(PoweredByRegistry.For(entity));
    }
}
