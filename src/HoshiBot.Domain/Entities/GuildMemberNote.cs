namespace HoshiBot.Domain.Entities;

// The live, injected community lore for one member of one guild — the structured, editable successor
// to the hand-written lore that used to live in the AI-chat system prompt. Keyed on Discord user id
// (survives nickname/alliance-tag changes). AiChatService injects a compact line per member so Hoshi
// can riff about the whole cast (the "ensemble" effect). See docs/ai-chat-member-lore.md.
//
// Fields split by author:
//  - Self-authored: the member edits these on the self-service page; extraction auto-fills empty ones.
//  - Peer/staff-authored: crowd/staff lore, review-gated before it lands here; the member can't edit
//    it but can hide it (PeerLoreHidden) as a trust safety valve.
public class GuildMemberNote
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong DiscordUserId { get; set; }

    // --- Self-authored (member-owned) ---

    // How Hoshi should address the member (their preferred name).
    public string? PreferredName { get; set; }

    // Other names/aliases the member goes by — also used to resolve peer-story targets.
    public string? Nicknames { get; set; }

    public string? Interests { get; set; }

    // Free "about" the member chose to share (job, real-life bits they're happy to have referenced).
    public string? Background { get; set; }

    public string? Languages { get; set; }

    // --- Peer/staff-authored (review-gated) ---

    // Running gags / affectionate teasing angles (e.g. "Frank + Rotwein", "Döni + Fettnäpfchen").
    public string? RunningJokes { get; set; }

    // What the member is happy to be teased about ("necken ok wegen …").
    public string? TeaseAbout { get; set; }

    // Member veto: when set, the peer-authored fields are excluded from the prompt injection and
    // flagged for staff — the "shared with the community ≠ OK for the bot to banter with" safety valve.
    public bool PeerLoreHidden { get; set; }

    public DateTimeOffset? SelfUpdatedAt { get; set; }

    public DateTimeOffset? PeerUpdatedAt { get; set; }
}
