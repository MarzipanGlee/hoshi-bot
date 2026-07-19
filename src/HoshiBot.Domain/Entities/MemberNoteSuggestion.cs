namespace HoshiBot.Domain.Entities;

// A review-gated draft for a GuildMemberNote — the "beat of review" for crowdsourced peer lore. When
// a member's interview yields a story about *someone else*, it lands here as Pending; a staff member
// approves (merging it into the target's note field), edits+approves, or rejects. Self-field values
// only become suggestions when auto-fill would otherwise overwrite member-curated text. See
// docs/ai-chat-member-lore.md.
public class MemberNoteSuggestion
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    // Resolved target member (whose note this merges into); null when the extracted name couldn't be
    // matched to a roster member — staff pick the target on review.
    public ulong? TargetDiscordUserId { get; set; }

    // The name as the extractor read it (e.g. "Döni") — shown to staff, and the basis for resolution.
    public string TargetNameRaw { get; set; } = "";

    public MemberNoteField Field { get; set; }

    public string SuggestedText { get; set; } = "";

    // Provenance: the interview it came from (SetNull if that interview is later deleted) and who told it.
    public int? SourceInterviewId { get; set; }

    public MemberInterview? SourceInterview { get; set; }

    public ulong SourceDiscordUserId { get; set; }

    public MemberNoteSuggestionStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
}
