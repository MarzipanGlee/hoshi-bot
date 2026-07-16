namespace HoshiBot.Domain.Entities;

// The distinct Command Bridge hub messages a guild can post. Each maps to its own channel +
// stored message id on GuildSettings and its own subset of buttons (see CommandBridgeCatalog):
// - User:    the member-facing "Kommandobrücke".
// - Staff:   "Kommandobrücke Führungsstab" — staff-only actions, posted to a staff channel.
// - Friends: "Kommandobrücke Freunde" — a trimmed subset for allied/friend guilds.
//
// Named ...Kind rather than CommandBridge to avoid colliding with the
// HoshiBot.Discord.CommandBridge namespace.
public enum CommandBridgeKind
{
    User,
    Staff,
    Friends,
}
