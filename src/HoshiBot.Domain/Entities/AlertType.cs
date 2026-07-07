namespace HoshiBot.Domain.Entities;

// StationMove isn't built yet — reserved so Alert stays the generic table
// covering every alert kind instead of a separate typed table per kind.
public enum AlertType
{
    Raid,
    StationMove,
}
