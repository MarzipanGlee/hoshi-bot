using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using Xunit;

namespace HoshiBot.Domain.Tests;

public class NicknameComposerTests
{
    private const int HomeAlliance = 1;
    private const int HomeServer = 164;
    private static readonly HashSet<int> HomeAlliances = [HomeAlliance];
    private static readonly HashSet<int> HomeServers = [HomeServer];

    // A member of the guild's own alliance on its own server, unless overridden.
    private static string Build(
        string name = "Almeophus",
        NicknameTagMode allianceMode = NicknameTagMode.ForeignOnly,
        NicknameTagMode serverMode = NicknameTagMode.ForeignOnly,
        int? allianceId = HomeAlliance,
        string? allianceTag = "SHQL",
        int serverId = HomeServer,
        string regionName = "EU",
        string? suffix = null) =>
        NicknameComposer.Build(name, regionName, serverId, allianceId, allianceTag,
            allianceMode, serverMode, HomeAlliances, HomeServers, suffix);

    [Fact]
    public void ForeignOnly_HomePlayer_GetsNoTags() =>
        Assert.Equal("Almeophus", Build());

    [Fact]
    public void ForeignOnly_ForeignAllianceAndServer_GetsBothTags() =>
        Assert.Equal("[EU999][BONK] Almeophus", Build(allianceId: 2, allianceTag: "BONK", serverId: 999));

    [Fact]
    public void Always_HomePlayer_GetsBothTags() =>
        Assert.Equal("[EU164][SHQL] Almeophus",
            Build(allianceMode: NicknameTagMode.Always, serverMode: NicknameTagMode.Always));

    [Fact]
    public void Never_ForeignPlayer_GetsNoTags() =>
        Assert.Equal("Almeophus",
            Build(allianceMode: NicknameTagMode.Never, serverMode: NicknameTagMode.Never,
                allianceId: 2, allianceTag: "BONK", serverId: 999));

    [Fact]
    public void AllianceLessPlayer_IsForeign_AndRendersNotApplicable() =>
        Assert.Equal("[n/a] Almeophus", Build(allianceId: null, allianceTag: null));

    [Fact]
    public void BlankRegion_SuppressesTheServerTag() =>
        Assert.Equal("[SHQL] Almeophus",
            Build(allianceMode: NicknameTagMode.Always, serverMode: NicknameTagMode.Always, regionName: ""));

    [Fact]
    public void Suffix_IsAppendedInParentheses() =>
        Assert.Equal("[SHQL] Almeophus (IgnisDraco)",
            Build(allianceMode: NicknameTagMode.Always, serverMode: NicknameTagMode.Never, suffix: "IgnisDraco"));

    [Fact]
    public void Suffix_IsDroppedWholeRatherThanTruncated()
    {
        // Both tags leave only 9 characters for " (…)" after the name — not enough, so the suffix
        // goes entirely rather than being cut into. This is the common case when a guild shows both
        // tags: the prefix alone already costs 14 characters of the 32.
        var result = Build(allianceMode: NicknameTagMode.Always, serverMode: NicknameTagMode.Always,
            suffix: "IgnisDraco");

        Assert.Equal("[EU164][SHQL] Almeophus", result);
        Assert.DoesNotContain("(", result);
    }

    [Fact]
    public void Suffix_FittingExactlyAt32_IsKept()
    {
        var result = Build(name: "Almeo", allianceMode: NicknameTagMode.Always,
            serverMode: NicknameTagMode.Always, suffix: "IgnisDrac");

        Assert.Equal("[EU164][SHQL] Almeo (IgnisDrac)", result);
        Assert.True(result.Length <= NicknameComposer.DiscordNicknameMaxLength);
    }

    [Fact]
    public void LongNameWithoutSuffix_IsStillTruncatedAt32()
    {
        var result = Build(name: new string('a', 40), allianceMode: NicknameTagMode.Always,
            serverMode: NicknameTagMode.Always);

        Assert.Equal(NicknameComposer.DiscordNicknameMaxLength, result.Length);
    }

    [Theory]
    [InlineData("  IgnisDraco  ", "IgnisDraco")]
    [InlineData("Ignis[Draco]", "IgnisDraco")]      // brackets would confuse the tag strip
    [InlineData("Ignis(Draco)", "IgnisDraco")]      // parens would confuse the suffix strip
    [InlineData("Ignis   Draco", "Ignis Draco")]    // inner whitespace collapses
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("()", null)]                        // nothing left after cleaning
    [InlineData(null, null)]
    public void CleanSuffix_Normalizes(string? raw, string? expected) =>
        Assert.Equal(expected, NicknameComposer.CleanSuffix(raw));

    [Fact]
    public void CleanSuffix_CapsLength() =>
        Assert.Equal(NicknameComposer.MaxSuffixLength,
            NicknameComposer.CleanSuffix(new string('x', 50))!.Length);

    // The property the player matcher depends on: whatever Build composes has to strip back to the
    // bare player name, or every member using a suffix falls out of auto-linking.
    [Theory]
    [InlineData(NicknameTagMode.Always, NicknameTagMode.Always, "IgnisDraco")]
    [InlineData(NicknameTagMode.Always, NicknameTagMode.Always, null)]
    [InlineData(NicknameTagMode.ForeignOnly, NicknameTagMode.Never, "IgnisDraco")]
    [InlineData(NicknameTagMode.Never, NicknameTagMode.Never, "IgnisDraco")]
    public void Strip_UndoesBuild(NicknameTagMode allianceMode, NicknameTagMode serverMode, string? suffix)
    {
        var composed = Build(allianceMode: allianceMode, serverMode: serverMode, suffix: suffix,
            allianceId: 2, allianceTag: "BONK", serverId: 999);

        Assert.Equal("Almeophus", NicknameComposer.Strip(composed));
    }

    [Fact]
    public void Strip_LeavesAnUntaggedNameAlone() =>
        Assert.Equal("Almeophus", NicknameComposer.Strip("Almeophus"));
}
