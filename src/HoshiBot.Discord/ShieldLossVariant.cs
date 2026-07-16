namespace HoshiBot.Discord;

// Which staff-reported shield-loss button was used — determines the reminder's expiration
// time (see AlertService.ResolveShieldExpirationAsync). Not persisted: it only shapes the
// ShieldExpiration set at creation, after which the reminder is a plain one.
public enum ShieldLossVariant
{
    Manual,
    InfiniteIncursions,
    TerritoryReset,
}
