using HoshiBot.Domain.Localization;

namespace HoshiBot.Domain.Tests;

// The expectation editor lists every Discord permission, and only the handful the bot asks for has
// a curated catalog label. The rest are derived from the enum name, so the derivation has to hold
// up for the names NetCord actually defines.
public class PermissionLabelTests
{
    [Theory]
    [InlineData("CreateInstantInvite", "Create Instant Invite")]
    [InlineData("KickUsers", "Kick Users")]
    [InlineData("Administrator", "Administrator")]        // single word, unchanged
    [InlineData("ViewAuditLog", "View Audit Log")]
    [InlineData("SendTtsMessages", "Send Tts Messages")]
    [InlineData("UseVoiceActivityDetection", "Use Voice Activity Detection")]
    [InlineData("PrioritySpeaker", "Priority Speaker")]
    [InlineData("Stream", "Stream")]
    public void DerivedLabel_SplitsOnWordBoundaries(string name, string expected) =>
        Assert.Equal(expected, Msg.WebAudit.Perm(Language.En, name));

    [Fact]
    public void CuratedLabel_WinsOverTheDerivedOne()
    {
        // ViewChannel has a catalog entry; it must not be re-derived into the same string by luck.
        Assert.Equal("View Channel", Msg.WebAudit.Perm(Language.En, "ViewChannel"));
        Assert.Equal("Kanal ansehen", Msg.WebAudit.Perm(Language.De, "ViewChannel"));
    }

    [Fact]
    public void AllCapsRun_KeepsItsAcronymTogether()
    {
        // No NetCord permission is named this way today; the rule should survive one that is.
        Assert.Equal("Use VAD Mode", Msg.WebAudit.Perm(Language.En, "UseVADMode"));
    }
}
