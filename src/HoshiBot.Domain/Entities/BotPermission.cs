namespace HoshiBot.Domain.Entities;

// The Discord permission bits the bot actually uses, as a Domain-level type so the per-feature
// declaration (GuildFeaturePermissions) can live here and be read by BOTH HoshiBot.Web (the audit
// page, the invite) and HoshiBot.Discord (the runtime permission reports). Neither Domain nor Data
// references NetCord, and that's worth keeping — so rather than pull the package in, this mirrors
// NetCord.Permissions.
//
// The values ARE Discord's own bit positions, which makes conversion at the two NetCord-facing
// edges a plain cast — (Permissions)(ulong)value — with no mapping table to drift. What guarantees
// that is BotPermissionTests.Values_match_NetCord_by_name, which reflects over every member here
// and asserts NetCord has one of the same name and value. That test is the entire safety story for
// the cast: if you add a member, add it there first and let it fail until the value is right.
//
// Only bits the bot can actually name in a declaration are listed. ManageMessages is a real
// requirement of ChannelAccessProfile.Draft (the announcement flow deletes a published draft and
// clears the reaction that published it, both on messages the bot did not write) — but only ever
// per channel, never in the invite baseline, which stays View/ManageChannels/ManageRoles.
[Flags]
public enum BotPermission : ulong
{
    None = 0,

    // Guild-level. Administrator isn't something the bot ever asks for — it's here because it
    // bypasses every channel overwrite, so permission math has to be able to name it.
    Administrator = 1UL << 3,
    ManageChannels = 1UL << 4,
    ManageNicknames = 1UL << 27,
    ManageRoles = 1UL << 28,

    // Channel-level.
    AddReactions = 1UL << 6,
    ViewChannel = 1UL << 10,
    SendMessages = 1UL << 11,
    ManageMessages = 1UL << 13,
    EmbedLinks = 1UL << 14,
    ReadMessageHistory = 1UL << 16,
    MentionEveryone = 1UL << 17,

    // Threads. Discord splits these finely: ManageThreads covers archiving/locking but NOT creating
    // a thread or posting in one, which is why the invite asking only for ManageThreads left
    // Tickets and RoE Violations relying on whatever broader grant a guild happened to give.
    //
    // CreatePublicThreads (1UL << 35) is deliberately absent: nothing creates a public thread on a
    // text channel, and it is NOT what a forum post needs — that's SendMessages, see
    // ChannelAccessProfile.ForumPosts. Discord doesn't even offer the bit on a forum channel.
    ManageThreads = 1UL << 34,
    CreatePrivateThreads = 1UL << 36,
    SendMessagesInThreads = 1UL << 38,

    // Split out of ManageMessages by Discord after the fact — a bot holding only ManageMessages
    // still gets a 403 on the pin call (this bit us once on the Territory Capture weekly digest).
    PinMessages = 1UL << 51,
}
