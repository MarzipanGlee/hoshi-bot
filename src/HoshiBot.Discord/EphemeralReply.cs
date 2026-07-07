using NetCord;
using NetCord.Rest;

namespace HoshiBot.Discord;

// Every reply that's just a reaction to the user's own action (a step in a modal/button
// flow, a personal confirmation, an error) should be ephemeral — only the user who acted
// needs to see it. The few genuinely public results (an announcement embed, a raid alert,
// a ticket thread) are separate messages posted directly to a channel/thread, not this
// kind of interaction reply, so they're unaffected by this helper.
public static class EphemeralReply
{
    public static InteractionMessageProperties Of(string content) =>
        new() { Content = content, Flags = MessageFlags.Ephemeral };
}
