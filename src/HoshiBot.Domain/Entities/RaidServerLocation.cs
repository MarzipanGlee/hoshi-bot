namespace HoshiBot.Domain.Entities;

// Whether the raided station is on the alliance's home server or an enemy server —
// only meaningful during Infinite Incursions events. Exactly these 2 values, no "unspecified"
// option, per explicit user decision.
public enum RaidServerLocation
{
    Home,
    Enemy,
}
