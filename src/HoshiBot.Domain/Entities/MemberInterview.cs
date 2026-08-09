namespace HoshiBot.Domain.Entities;

// One member's DM "interview" by the bot — the member-lore collection step: Hoshi DMs the member and
// chats them up in character to learn who they are, what to call them, and stories about others. The
// transcript lives in MemberInterviewMessage; it's the raw material the later note-extraction step
// turns into community lore. Created when the opener DM is sent. See docs/ai-chat-member-lore.md.
public class MemberInterview
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    // The linked alliance (GuildAlliance.Id) this interview was started for — used to resolve the
    // per-alliance MemberLore settings (e.g. the completed role) when the interview finishes.
    // The scope that invited this member. GuildAllianceId alone was enough while Member Lore was
    // an alliance-only feature; with every audience able to run it, a null alliance no longer means
    // "no scope" — it means one of the three audiences that have no alliance.
    public GuildAudience Audience { get; set; }

    public int? GuildAllianceId { get; set; }

    public ulong DiscordUserId { get; set; }

    public MemberInterviewStatus Status { get; set; }

    // The member's DM channel — cached so replies can be matched back and the closer posted.
    public ulong? DmChannelId { get; set; }

    // The language the member replies in (the bot mirrors it); null until detected from a reply.
    public string? Language { get; set; }

    public DateTimeOffset InvitedAt { get; set; }

    public DateTimeOffset LastActivityAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    // Set once the note-extraction job has turned this (completed) transcript into member notes /
    // suggestions — so each interview is extracted exactly once. Null = not yet extracted.
    public DateTimeOffset? ExtractedAt { get; set; }

    public ICollection<MemberInterviewMessage> Messages { get; set; } = [];
}
