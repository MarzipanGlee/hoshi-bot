using HoshiBot.Domain;

namespace HoshiBot.Domain.Tests;

public class DiscordMentionTextTests
{
    // The real channel names from the guild this was found in — emoji and a box-drawing separator,
    // which is precisely why the matcher can't be a conservative [a-z0-9-].
    private static readonly Dictionary<string, ulong> Channels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["💬│tipps-chat"] = 111,
        ["💬│allianz-chat"] = 222,
        ["🏁│einführung"] = 333,
        ["willkommen"] = 444,
    };

    // "Command" as well as "Command Staff": the shorter name must not win, or the longer mention
    // leaves " Staff" stranded as text.
    private static readonly Dictionary<string, ulong> Roles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Command Staff"] = 900,
        ["Command"] = 901,
        ["Diplomat"] = 902,
        ["everyone"] = 999,
    };

    private static string Run(string? text) => DiscordMentionText.Normalize(text, Channels, Roles);

    // The bug as it reached production: six "channels" in the guide, none of them clickable,
    // because copying a pill out of Discord yields its display text plus an invisible word joiner.
    [Fact]
    public void A_pasted_channel_pill_becomes_a_real_mention() =>
        Assert.Equal("- <#111>", Run("- #⁠💬│tipps-chat"));

    [Fact]
    public void A_typed_channel_name_becomes_a_real_mention() =>
        Assert.Equal("see <#444> first", Run("see #willkommen first"));

    [Fact]
    public void A_pasted_channel_id_becomes_a_real_mention()
    {
        Assert.Equal("<#849389684933394432>", Run("#⁠849389684933394432"));
        Assert.Equal("<#849389684933394432>", Run("#@849389684933394432"));
    }

    [Fact]
    public void A_pasted_role_pill_becomes_a_real_mention() =>
        Assert.Equal("ask <@&902>", Run("ask @⁠Diplomat"));

    // A role name with a space is the case a non-space run can't handle at all, which is why roles
    // are matched against the guild's actual names instead of by shape.
    [Fact]
    public void A_role_name_containing_spaces_is_matched_whole() =>
        Assert.Equal("<@&900> can help", Run("@Command Staff can help"));

    [Fact]
    public void The_longest_matching_role_name_wins() =>
        Assert.Equal("<@&900>", Run("@Command Staff"));

    [Fact]
    public void A_shorter_role_still_matches_on_its_own() =>
        Assert.Equal("<@&901> only", Run("@Command only"));

    // Converting these would be both wrong and, in a channel post rather than an embed, dangerous.
    [Fact]
    public void Everyone_and_here_are_never_converted() =>
        Assert.Equal("@everyone @here", Run("@everyone @here"));

    [Fact]
    public void Existing_mentions_are_left_alone() =>
        Assert.Equal("<#999> <@&5> <@7> <#111>", Run("<#999> <@&5> <@7> #💬│tipps-chat"));

    // Guessing here would be worse than doing nothing: text that merely starts with "#" or "@" is
    // usually just text, and turning "#1" into a mention would corrupt the message.
    [Fact]
    public void Unresolvable_text_is_left_untouched() =>
        Assert.Equal("#nope and #1 and @nobody", Run("#nope and #1 and @nobody"));

    [Fact]
    public void A_markdown_heading_survives() =>
        Assert.Equal("# Heading\n## Sub", Run("# Heading\n## Sub"));

    [Fact]
    public void Invisible_characters_are_stripped_even_where_nothing_resolves() =>
        Assert.Equal("plain text", Run("plain​ text⁠"));

    [Fact]
    public void Null_and_empty_are_handled()
    {
        Assert.Equal("", Run(null));
        Assert.Equal("", Run(""));
    }

    // A guild with no roles at all must not build an empty alternation, which would match the empty
    // string after every "@" and rewrite the text into nonsense.
    [Fact]
    public void An_empty_role_list_is_safe() =>
        Assert.Equal("@Command Staff and <#111>",
            DiscordMentionText.Normalize("@Command Staff and #💬│tipps-chat", Channels, new Dictionary<string, ulong>()));

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
            Run(pasted));
    }
}
