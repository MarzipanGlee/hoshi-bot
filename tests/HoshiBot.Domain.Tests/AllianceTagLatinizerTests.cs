using HoshiBot.Domain;
using Xunit;

namespace HoshiBot.Domain.Tests;

public class AllianceTagLatinizerTests
{
    [Theory]
    // The two shapes the feature was asked for.
    [InlineData("ĶÅØŞ", "KAOS")]
    [InlineData("ВСТК", "BCTK")]
    // Real tags from the alliance catalog.
    [InlineData("ЯAID", "RAID")]
    [InlineData("BØRG", "BORG")]
    [InlineData("ÆGIS", "AEGIS")]
    [InlineData("ΞOOΞ", "EOOE")]
    [InlineData("DÀRK", "DARK")]
    [InlineData("ĐØM", "DOM")]
    [InlineData("BLÜD", "BLUD")]
    [InlineData("NÏTE", "NITE")]
    [InlineData("VGĖR", "VGER")]
    [InlineData("CΦΕ", "CFE")]
    // Already plain — untouched.
    [InlineData("SITH", "SITH")]
    [InlineData("", "")]
    public void ToLatin_MapsLookalikes(string tag, string expected) =>
        Assert.Equal(expected, AllianceTagLatinizer.ToLatin(tag));

    [Fact]
    public void ToLatin_LeavesScriptsWithNoLookalikeAlone()
    {
        // CJK/Kana/Hangul have no look-alike Latin form; mangling them would be worse than
        // leaving the tag as it is, so such a tag just keeps its own role. Mixed tags are
        // latinized as far as they can be — here the Greek Σ becomes S, the rest stays.
        Assert.Equal("乃IS尺", AllianceTagLatinizer.ToLatin("乃IΣ尺"));
        Assert.Equal("ㅇVㅌ", AllianceTagLatinizer.ToLatin("ㅇVㅌ"));
        Assert.Equal("TSPツ", AllianceTagLatinizer.ToLatin("TSPツ"));
    }

    [Fact]
    public void ToLatin_IsCasePreserving()
    {
        Assert.Equal("BORG", AllianceTagLatinizer.ToLatin("BØRG"));
        Assert.Equal("borg", AllianceTagLatinizer.ToLatin("børg"));
    }

    [Fact]
    public void ToLatin_CollapsesHomoglyphs()
    {
        // JАG with a Cyrillic А is a real catalog tag, distinct from the ASCII JAG. Latinizing
        // makes them the same string — so with the option on they share one role, which is the
        // point (nobody can tell them apart on screen anyway).
        Assert.Equal("JAG", AllianceTagLatinizer.ToLatin("JАG"));
        Assert.Equal(AllianceTagLatinizer.ToLatin("JAG"), AllianceTagLatinizer.ToLatin("JАG"));
    }

    [Fact]
    public void ToLatin_DoesNotCollapseRepeats()
    {
        // Unlike SystemNamePhoneticKey, which folds doubled letters — that would turn this tag
        // into a single character.
        Assert.Equal("OO", AllianceTagLatinizer.ToLatin("ØØ"));
    }

    [Theory]
    [InlineData("BØRG", true)]
    [InlineData("ВСТК", true)]
    [InlineData("SITH", false)]
    public void NeedsLatinizing_DetectsChange(string tag, bool expected) =>
        Assert.Equal(expected, AllianceTagLatinizer.NeedsLatinizing(tag));
}

public class AllianceTagRoleNameTests
{
    [Theory]
    [InlineData("KAOS", false, false, null, null, "KAOS")]
    [InlineData("ĶÅØŞ", true, false, null, null, "KAOS")]
    [InlineData("ĶÅØŞ", false, false, null, null, "ĶÅØŞ")]
    [InlineData("KAOS", false, true, null, null, "kaos")]
    [InlineData("KAOS", false, false, "[", "]", "[KAOS]")]
    [InlineData("ĶÅØŞ", true, true, "alliance-", null, "alliance-kaos")]
    public void Build_AppliesOptions(string tag, bool latinize, bool lowercase, string? prefix, string? suffix, string expected) =>
        Assert.Equal(expected, AllianceTagRoleName.Build(tag, latinize, lowercase, prefix, suffix));

    [Fact]
    public void Build_ClampsToDiscordLimit()
    {
        var name = AllianceTagRoleName.Build("TAG", false, false, new string('x', 120), null);

        Assert.Equal(AllianceTagRoleName.MaxRoleNameLength, name.Length);
    }

    [Fact]
    public void Candidates_IncludeBothSpellings()
    {
        var candidates = AllianceTagRoleName.Candidates("ВСТК", latinize: true, lowercase: false, null, null);

        Assert.Equal(["BCTK", "ВСТК"], candidates);
    }

    [Fact]
    public void Candidates_AreTheSameSetWhicheverWayLatinizeIsSet()
    {
        var on = AllianceTagRoleName.Candidates("ВСТК", latinize: true, lowercase: false, null, null);
        var off = AllianceTagRoleName.Candidates("ВСТК", latinize: false, lowercase: false, null, null);

        // Same names, opposite preference order — so flipping the option reuses existing roles
        // rather than creating a second set.
        Assert.Equal(on.OrderBy(x => x), off.OrderBy(x => x));
        Assert.NotEqual(on[0], off[0]);
    }

    [Fact]
    public void Candidates_CollapseWhenLatinizingChangesNothing() =>
        Assert.Single(AllianceTagRoleName.Candidates("SITH", latinize: true, lowercase: false, null, null));
}
