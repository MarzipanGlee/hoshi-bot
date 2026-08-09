using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using Xunit;

namespace HoshiBot.Domain.Tests;

public class NicknameComposerTests
{
    private const int HomeAlliance = 1;
    private const int HomeServer = 1;
    private static readonly HashSet<int> HomeAlliances = [HomeAlliance];
    private static readonly HashSet<int> HomeServers = [HomeServer];

    // A member of the guild's own alliance on its own server, unless overridden.
    private static string Build(
        string name = "Player",
        NicknameTagMode allianceMode = NicknameTagMode.ForeignOnly,
        NicknameTagMode serverMode = NicknameTagMode.ForeignOnly,
        int? allianceId = HomeAlliance,
        string? allianceTag = "TAG",
        int serverId = HomeServer,
        string regionName = "RG",
        string? suffix = null) =>
        NicknameComposer.Build(name, regionName, serverId, allianceId, allianceTag,
            allianceMode, serverMode, HomeAlliances, HomeServers, suffix);

    [Fact]
    public void ForeignOnly_HomePlayer_GetsNoTags() =>
        Assert.Equal("Player", Build());

    [Fact]
    public void ForeignOnly_ForeignAllianceAndServer_GetsBothTags() =>
        Assert.Equal("[RG-99][OTHR] Player", Build(allianceId: 2, allianceTag: "OTHR", serverId: 99));

    [Fact]
    public void Always_HomePlayer_GetsBothTags() =>
        Assert.Equal("[RG-1][TAG] Player",
            Build(allianceMode: NicknameTagMode.Always, serverMode: NicknameTagMode.Always));

    [Fact]
    public void Never_ForeignPlayer_GetsNoTags() =>
        Assert.Equal("Player",
            Build(allianceMode: NicknameTagMode.Never, serverMode: NicknameTagMode.Never,
                allianceId: 2, allianceTag: "OTHR", serverId: 99));

    [Fact]
    public void AllianceLessPlayer_IsForeign_AndRendersNotApplicable() =>
        Assert.Equal("[n/a] Player", Build(allianceId: null, allianceTag: null));

    [Fact]
    public void BlankRegion_SuppressesTheServerTag() =>
        Assert.Equal("[TAG] Player",
            Build(allianceMode: NicknameTagMode.Always, serverMode: NicknameTagMode.Always, regionName: ""));

    [Fact]
    public void Suffix_IsAppendedInParentheses() =>
        Assert.Equal("[TAG] Player (Alias)",
            Build(allianceMode: NicknameTagMode.Always, serverMode: NicknameTagMode.Never, suffix: "Alias"));

    [Fact]
    public void Suffix_IsDroppedWholeRatherThanTruncated()
    {
        // The suffix goes entirely rather than being cut into. Real tags cost far more than this
        // fixture's — a five-letter region+server and a four-letter alliance tag already eat 14 of
        // the 32 characters — so this is the common case once a guild shows both tags.
        var result = Build(allianceMode: NicknameTagMode.Always, serverMode: NicknameTagMode.Always,
            suffix: "AliasThatIsTooLong");

        Assert.Equal("[RG-1][TAG] Player", result);
        Assert.DoesNotContain("(", result);
    }

    [Fact]
    public void Suffix_FittingExactlyAt32_IsKept()
    {
        var result = Build(allianceMode: NicknameTagMode.Always,
            serverMode: NicknameTagMode.Always, suffix: "AliasTwelve");

        Assert.Equal("[RG-1][TAG] Player (AliasTwelve)", result);
        Assert.True(result.Length <= NicknameComposer.DiscordNicknameMaxLength);
    }

    [Fact]
    public void NoAllianceTag_DefaultsToLowercaseNa()
    {
        Assert.Equal("[n/a] Player", Build(allianceId: null, allianceTag: null));
    }

    [Fact]
    public void NoAllianceTag_UsesTheGuildsOwnTextWhenSet()
    {
        var result = NicknameComposer.Build("Player", "RG", 1, allianceId: null, allianceTag: null,
            NicknameTagMode.Always, NicknameTagMode.Never, [], [], suffix: null, noAllianceTag: "solo");

        Assert.Equal("[solo] Player", result);
    }

    [Fact]
    public void NoAllianceTag_BlankFallsBackToTheDefault()
    {
        // Blank is stored as null by the editor, but a whitespace value must not produce "[ ]".
        var result = NicknameComposer.Build("Player", "RG", 1, allianceId: null, allianceTag: null,
            NicknameTagMode.Always, NicknameTagMode.Never, [], [], suffix: null, noAllianceTag: "   ");

        Assert.Equal("[n/a] Player", result);
    }

    [Fact]
    public void ServerTag_IsRegionAndServerHyphenated()
    {
        // "EU-164", not "EU164": two facts, not one token. StfcServer.DisplayName keeps its own
        // "EU164 Mindmeld" convention for dropdowns — these are deliberately different.
        Assert.Equal("[RG-164] Player",
            NicknameComposer.Build("Player", "RG", 164, allianceId: 1, allianceTag: "TAG",
                NicknameTagMode.Never, NicknameTagMode.Always, [1], []));
    }

    [Fact]
    public void LongNameWithoutSuffix_IsStillTruncatedAt32()
    {
        var result = Build(name: new string('a', 40), allianceMode: NicknameTagMode.Always,
            serverMode: NicknameTagMode.Always);

        Assert.Equal(NicknameComposer.DiscordNicknameMaxLength, result.Length);
    }

    [Theory]
    [InlineData("  Alias  ", "Alias")]
    [InlineData("Ali[as]", "Alias")]          // brackets would confuse the tag strip
    [InlineData("Ali(as)", "Alias")]          // parens would confuse the suffix strip
    [InlineData("My   Alias", "My Alias")]    // inner whitespace collapses
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
    [InlineData(NicknameTagMode.Always, NicknameTagMode.Always, "Alias")]
    [InlineData(NicknameTagMode.Always, NicknameTagMode.Always, null)]
    [InlineData(NicknameTagMode.ForeignOnly, NicknameTagMode.Never, "Alias")]
    [InlineData(NicknameTagMode.Never, NicknameTagMode.Never, "Alias")]
    public void Strip_UndoesBuild(NicknameTagMode allianceMode, NicknameTagMode serverMode, string? suffix)
    {
        var composed = Build(allianceMode: allianceMode, serverMode: serverMode, suffix: suffix,
            allianceId: 2, allianceTag: "OTHR", serverId: 99);

        Assert.Equal("Player", NicknameComposer.Strip(composed));
    }

    [Fact]
    public void Strip_LeavesAnUntaggedNameAlone() =>
        Assert.Equal("Player", NicknameComposer.Strip("Player"));
}
