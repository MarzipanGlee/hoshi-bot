using HoshiBot.Domain.Entities;
using Scope = HoshiBot.Domain.Entities.BoardingScopes.Scope;

namespace HoshiBot.Domain.Tests;

public class BoardingScopesTests
{
    private static Scope Alliance(int id) => new(GuildAudience.Alliance, id);
    private static readonly Scope Server = new(GuildAudience.Server, null);
    private static readonly Scope Community = new(GuildAudience.Community, null);

    [Fact]
    public void NoScopeEnabled_ClaimsNobody() =>
        Assert.Null(BoardingScopes.Claim([], memberGuildAllianceId: 1));

    [Fact]
    public void Community_ClaimsAMemberWithNoLinkedPlayer() =>
        Assert.Equal(Community, BoardingScopes.Claim([Community], memberGuildAllianceId: null));

    [Fact]
    public void Alliance_DoesNotClaimAnUnlinkedMember() =>
        Assert.Null(BoardingScopes.Claim([Alliance(1)], memberGuildAllianceId: null));

    [Fact]
    public void Alliance_DoesNotClaimSomeoneElsesAllianceMember() =>
        Assert.Null(BoardingScopes.Claim([Alliance(1)], memberGuildAllianceId: 2));

    [Fact]
    public void Alliance_BeatsCommunity_ForItsOwnMember() =>
        Assert.Equal(Alliance(1), BoardingScopes.Claim([Community, Alliance(1)], memberGuildAllianceId: 1));

    [Fact]
    public void Community_TakesTheMembersTheAllianceDoesNotClaim() =>
        Assert.Equal(Community, BoardingScopes.Claim([Community, Alliance(1)], memberGuildAllianceId: 2));

    [Fact]
    public void Server_BeatsCommunity()
    {
        // Both claim everyone; the order is what decides, not which was enabled first.
        Assert.Equal(Server, BoardingScopes.Claim([Community, Server], memberGuildAllianceId: null));
    }

    [Fact]
    public void EachAllianceClaimsItsOwn()
    {
        var enabled = new[] { Alliance(1), Alliance(2) };
        Assert.Equal(Alliance(2), BoardingScopes.Claim(enabled, memberGuildAllianceId: 2));
        Assert.Equal(Alliance(1), BoardingScopes.Claim(enabled, memberGuildAllianceId: 1));
    }

    [Fact]
    public void OrderIsNarrowestFirst() =>
        Assert.Equal(
            [GuildAudience.Alliance, GuildAudience.Server, GuildAudience.VeilGroup, GuildAudience.Community],
            BoardingScopes.Order);
}
