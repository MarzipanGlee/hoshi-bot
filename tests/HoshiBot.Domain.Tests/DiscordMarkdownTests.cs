using HoshiBot.Domain;
using Xunit;

namespace HoshiBot.Domain.Tests;

public class DiscordMarkdownTests
{
    // The reported case: the model linked a channel URL to itself, which Discord rendered
    // verbatim (brackets and all) in the plain answer message.
    [Fact]
    public void NormalizeForPlainMessage_SelfLinkedChannelUrl_BecomesChannelMention()
    {
        var result = DiscordMarkdown.NormalizeForPlainMessage(
            "Ich kann euch auf den PvP-Crew-Kanal verweisen: "
            + "[https://discord.com/channels/938079868204220456/1244997934064341093](https://discord.com/channels/938079868204220456/1244997934064341093).");

        Assert.Equal("Ich kann euch auf den PvP-Crew-Kanal verweisen: <#1244997934064341093>.", result);
    }

    [Theory]
    [InlineData("[PvP-Crews](https://discord.com/channels/938079868204220456/1244997934064341093)")]
    [InlineData("[#pvp-crews](https://discordapp.com/channels/938079868204220456/1244997934064341093)")]
    [InlineData("[hier](https://canary.discord.com/channels/938079868204220456/1244997934064341093)")]
    public void NormalizeForPlainMessage_MaskedChannelLink_DropsLabelForMention(string input)
    {
        Assert.Equal("<#1244997934064341093>", DiscordMarkdown.NormalizeForPlainMessage(input));
    }

    [Fact]
    public void NormalizeForPlainMessage_BareChannelUrl_BecomesChannelMention()
    {
        var result = DiscordMarkdown.NormalizeForPlainMessage(
            "Schau in https://discord.com/channels/938079868204220456/1244997934064341093 nach.");

        Assert.Equal("Schau in <#1244997934064341093> nach.", result);
    }

    // A jump link points at one specific message, which <#id> cannot express — Discord renders a
    // bare URL as clickable, so it stays as-is.
    [Fact]
    public void NormalizeForPlainMessage_MessageJumpLink_IsLeftAlone()
    {
        const string input = "Siehe https://discord.com/channels/938079868204220456/1244997934064341093/1300000000000000000";

        Assert.Equal(input, DiscordMarkdown.NormalizeForPlainMessage(input));
    }

    [Fact]
    public void NormalizeForPlainMessage_MaskedJumpLink_KeepsLabelAndUrl()
    {
        var result = DiscordMarkdown.NormalizeForPlainMessage(
            "[die Ankündigung](https://discord.com/channels/1/2/3) ist raus");

        Assert.Equal("die Ankündigung (https://discord.com/channels/1/2/3) ist raus", result);
    }

    [Fact]
    public void NormalizeForPlainMessage_DescriptiveExternalLink_BecomesTextWithUrlInParens()
    {
        var result = DiscordMarkdown.NormalizeForPlainMessage("Schau mal ins [PvP-Crew-Video](https://youtu.be/abc123).");

        Assert.Equal("Schau mal ins PvP-Crew-Video (https://youtu.be/abc123).", result);
    }

    [Theory]
    [InlineData("[https://youtu.be/abc](https://youtu.be/abc)", "https://youtu.be/abc")]
    [InlineData("[](https://youtu.be/abc)", "https://youtu.be/abc")]
    public void NormalizeForPlainMessage_LinkWithoutUsefulLabel_CollapsesToUrl(string input, string expected)
    {
        Assert.Equal(expected, DiscordMarkdown.NormalizeForPlainMessage(input));
    }

    [Fact]
    public void NormalizeForPlainMessage_SeveralLinks_AllRewritten()
    {
        var result = DiscordMarkdown.NormalizeForPlainMessage(
            "Crews stehen in [#pvp-crews](https://discord.com/channels/1/22), Videos im [Guide](https://youtu.be/x).");

        Assert.Equal("Crews stehen in <#22>, Videos im Guide (https://youtu.be/x).", result);
    }

    // Existing mention/timestamp tokens and plain prose must survive untouched.
    [Theory]
    [InlineData("Hey <@123>, schau in <#456> — Wartung ist <t:1700000000:t>.")]
    [InlineData("Kein Link, nur Text [in eckigen Klammern] und (in Klammern).")]
    [InlineData("")]
    public void NormalizeForPlainMessage_NothingToRewrite_ReturnsInputUnchanged(string input)
    {
        Assert.Equal(input, DiscordMarkdown.NormalizeForPlainMessage(input));
    }

    // The streaming path renders partial text: a link that isn't closed yet must be left alone
    // (the next pass, or FinalizeAnswer, handles it once complete).
    [Fact]
    public void NormalizeForPlainMessage_HalfStreamedLink_IsLeftAlone()
    {
        const string input = "Schau mal ins [PvP-Crew-Video](https://youtu.be/abc";

        Assert.Equal(input, DiscordMarkdown.NormalizeForPlainMessage(input));
    }
}
