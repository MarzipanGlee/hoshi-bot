using HoshiBot.Domain;

namespace HoshiBot.Domain.Tests;

public class ChannelMentionTextTests
{
    // The real channel names from the guild this was found in — emoji and a box-drawing separator,
    // which is precisely why the matcher can't be a conservative [a-z0-9-].
    private static ulong? Resolve(string name) => name switch
    {
        "💬│tipps-chat" => 111UL,
        "💬│allianz-chat" => 222UL,
        "🏁│einführung" => 333UL,
        "willkommen" => 444UL,
        _ => null,
    };

    // The bug as it reached production: six "channels" in the guide, none of them clickable,
    // because copying a pill out of Discord yields its display text plus an invisible word joiner.
    [Fact]
    public void A_pasted_channel_pill_becomes_a_real_mention()
    {
        Assert.Equal("- <#111>", ChannelMentionText.Normalize("- #⁠💬│tipps-chat", Resolve));
    }

    [Fact]
    public void A_typed_name_becomes_a_real_mention()
    {
        Assert.Equal("see <#444> first", ChannelMentionText.Normalize("see #willkommen first", Resolve));
    }

    [Fact]
    public void A_pasted_id_becomes_a_real_mention()
    {
        Assert.Equal("<#849389684933394432>", ChannelMentionText.Normalize("#⁠849389684933394432", Resolve));
        Assert.Equal("<#849389684933394432>", ChannelMentionText.Normalize("#@849389684933394432", Resolve));
    }

    [Fact]
    public void An_existing_mention_is_left_alone()
    {
        Assert.Equal("<#999> and <#111>", ChannelMentionText.Normalize("<#999> and #💬│tipps-chat", Resolve));
    }

    // Guessing here would be worse than doing nothing: text that merely starts with "#" is usually
    // just text, and turning "#1" into a mention would corrupt the message.
    [Fact]
    public void Unresolvable_text_is_left_untouched()
    {
        Assert.Equal("#nope and #1", ChannelMentionText.Normalize("#nope and #1", Resolve));
    }

    // A markdown heading is safe because the space ends the run before it begins.
    [Fact]
    public void A_markdown_heading_survives()
    {
        Assert.Equal("# Heading\n## Sub", ChannelMentionText.Normalize("# Heading\n## Sub", Resolve));
    }

    [Fact]
    public void Invisible_characters_are_stripped_even_where_nothing_resolves()
    {
        Assert.Equal("plain text", ChannelMentionText.Normalize("plain​ text⁠", Resolve));
    }

    [Fact]
    public void Null_and_empty_are_handled()
    {
        Assert.Equal("", ChannelMentionText.Normalize(null, Resolve));
        Assert.Equal("", ChannelMentionText.Normalize("", Resolve));
    }

    // The whole legacy message, end to end — every line that names a channel should come out
    // clickable, and the prose in between should be untouched.
    [Fact]
    public void The_whole_guide_message_is_repaired()
    {
        const string pasted = "Frage in einem der folgenden Kanälen:\n\n"
            + "- #⁠💬│tipps-chat\n- #⁠💬│allianz-chat\n\n"
            + "die Nachricht in ⁠#🏁│einführung genau zu lesen!";

        Assert.Equal("Frage in einem der folgenden Kanälen:\n\n"
            + "- <#111>\n- <#222>\n\n"
            + "die Nachricht in <#333> genau zu lesen!",
            ChannelMentionText.Normalize(pasted, Resolve));
    }
}
