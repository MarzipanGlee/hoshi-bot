namespace HoshiBot.Domain.Entities;

public enum MemberInterviewStatus
{
    Invited,       // opener DM sent, awaiting the member's first reply
    InProgress,    // member has replied at least once
    Completed,     // interview wrapped up
    Declined,      // member opted out
    Undeliverable, // DMs closed — the opener couldn't be sent
}
