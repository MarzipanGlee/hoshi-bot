namespace HoshiBot.Domain.Entities;

// Matches STFC's in-game alliance diplomacy statuses. Used by StfcAllianceDiplomacy
// (one alliance's stance toward another) — not a guild-level concept.
public enum DiplomacyStatus
{
    Allied,
    Friendly,
    Civil,
    Neutral,
    Caution,
    Unfriendly,
    Enemy,
}
