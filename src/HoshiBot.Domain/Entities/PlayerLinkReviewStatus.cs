namespace HoshiBot.Domain.Entities;

public enum PlayerLinkReviewStatus
{
    // The matcher couldn't confidently link this member (0 or >1 candidates, or a single
    // out-of-alliance match) — waiting for an admin (or the opt-in MemberOnboarding DM) to resolve it.
    Unresolved,

    // A UserPlayer link was created for this member (by an admin via the table, or the member via a DM).
    Resolved,

    // An admin explicitly dismissed this row (e.g. the member is intentionally unlinked) — never re-processed.
    Ignored,

    // MemberOnboarding only: the confirmation/picker DM has been sent, awaiting the member's response.
    DmSent,

    // MemberOnboarding only: the member declined the DM outreach.
    Declined,

    // MemberOnboarding only: the member's DMs are closed, so the outreach DM couldn't be delivered.
    Undeliverable,
}
