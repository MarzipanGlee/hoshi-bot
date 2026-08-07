namespace HoshiBot.Domain.Entities;

// What the bot *does* in a channel, as a named shape rather than a raw bitmask. There are ~40
// configured channel slots across all features and only these eight behaviours between them, so a
// name earns its keep three times over: each declaration arm in GuildFeaturePermissions reads as
// intent instead of restating the same bit union for the fortieth time, the audit page and the
// runtime failure report have something to *call* the shape, and when Discord next splits a bit out
// of a broader one (it has done so twice — PinMessages out of ManageMessages, SendMessagesInThreads
// out of SendMessages) exactly one profile changes instead of twenty slots.
//
// There is deliberately no "profile plus a few extra bits" escape hatch. If a slot needs a shape
// none of these has, add a profile — PostPinAndPing is exactly that case, and it is more useful as
// a name than as an inline union nobody can search for.
public enum ChannelAccessProfile
{
    None = 0,

    // The bot reads here and never writes: AI chat knowledge sources, and the announcement
    // forwarder's source channels. Auditing these as post targets (which is what happened before
    // this existed) tells a correctly-configured guild it has a problem.
    Read,

    // Reads history for context and answers in plain text — no embed, so no Embed Links. A listen
    // channel missing Read Message History is the classic "the bot just stays silent" case.
    Reply,

    // The default. Almost everything the bot posts is a branded embed (EmbedBranding), and a
    // channel that grants Send but denies Embed Links passes a naive View+Send check and then
    // fails at post time.
    Post,

    // Posts and pings a role. Needed because the roles features ping (notification roles, zone
    // slot roles, alert roles) are usually set non-mentionable, which is exactly when
    // Mention Everyone becomes required. Harmless to hold when the role IS mentionable.
    PostAndPing,

    // As above, and pins its own message — the Territory Capture weekly digest. Discord split
    // pinning out of Manage Messages, so a bot holding only Manage Messages still 403s on the pin;
    // that once made the whole digest look failed and resent every 30 minutes, double-pinging.
    PostPinAndPing,

    // The announcements draft channel: staff write drafts, the bot reads them back to publish,
    // decorates each with the four severity reactions, and clears up after a publish. Without Add
    // Reactions nothing errors — drafts simply never get decorated and staff have no way to publish
    // at all. Without Manage Messages, publishing leaves the draft behind and the clicked reaction
    // on it, which is likewise silent.
    Draft,

    // Opens a private thread per ticket on a normal text channel and talks inside it. Note what is
    // NOT here: SendMessages on the parent channel. The bot posts in the thread, never in the
    // channel itself.
    PrivateThreads,

    // Opens a forum post per report, talks inside it and pings a role there.
    //
    // Forums do NOT work like text channels here, and the difference is easy to get wrong: creating
    // a forum post is SendMessages, not CreatePublicThreads. Discord's own flag doc says so —
    // "Allows for sending messages in a channel and creating threads in a forum" — and a forum
    // channel's permission UI reflects it by labelling SendMessages "Create Posts" and not offering
    // Create Public Threads at all. Requiring CreatePublicThreads here made the audit ask for a
    // permission the admin could not find, while ignoring the one they had already granted.
    ForumPosts,
}

public static class ChannelAccessProfiles
{
    public static BotPermission Permissions(this ChannelAccessProfile profile) => profile switch
    {
        ChannelAccessProfile.Read =>
            BotPermission.ViewChannel | BotPermission.ReadMessageHistory,

        ChannelAccessProfile.Reply =>
            BotPermission.ViewChannel | BotPermission.SendMessages | BotPermission.ReadMessageHistory,

        ChannelAccessProfile.Post =>
            BotPermission.ViewChannel | BotPermission.SendMessages | BotPermission.EmbedLinks,

        ChannelAccessProfile.PostAndPing =>
            ChannelAccessProfile.Post.Permissions() | BotPermission.MentionEveryone,

        ChannelAccessProfile.PostPinAndPing =>
            ChannelAccessProfile.PostAndPing.Permissions() | BotPermission.PinMessages,

        // Manage Messages is the one permission here that acts on somebody ELSE's message: removing
        // the draft once it has been published, and clearing the severity reaction the staff member
        // clicked. Both are things the bot does on a message it did not write, and neither is
        // possible without it. Scoped to this one channel by the declaration, not granted
        // guild-wide — "delete anyone's messages anywhere" would be far too much for it.
        ChannelAccessProfile.Draft =>
            ChannelAccessProfile.Post.Permissions() | BotPermission.ReadMessageHistory | BotPermission.AddReactions | BotPermission.ManageMessages,

        ChannelAccessProfile.PrivateThreads =>
            BotPermission.ViewChannel | BotPermission.EmbedLinks | BotPermission.CreatePrivateThreads
            | BotPermission.SendMessagesInThreads | BotPermission.ManageThreads,

        ChannelAccessProfile.ForumPosts =>
            BotPermission.ViewChannel | BotPermission.SendMessages | BotPermission.EmbedLinks
            | BotPermission.MentionEveryone | BotPermission.SendMessagesInThreads | BotPermission.ManageThreads,

        _ => BotPermission.None,
    };
}
