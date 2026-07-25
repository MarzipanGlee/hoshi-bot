namespace HoshiBot.Domain.Entities;

// Priority an alliance assigns to one of a zone's services in its Service Selection — the two
// buckets the "Dienste aktivieren" reminder groups by (legacy's obligatorisch/optional split).
public enum TerritoryServicePriority
{
    MustHave,
    NiceToHave,
}
