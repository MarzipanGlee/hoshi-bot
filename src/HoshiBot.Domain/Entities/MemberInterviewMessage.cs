namespace HoshiBot.Domain.Entities;

// One line of a member interview transcript (in order by CreatedAt) — the persisted Q&A the
// note-extraction step reads.
public class MemberInterviewMessage
{
    public int Id { get; set; }

    public int InterviewId { get; set; }

    public MemberInterview Interview { get; set; } = null!;

    public MemberInterviewRole Role { get; set; }

    public string Content { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}
