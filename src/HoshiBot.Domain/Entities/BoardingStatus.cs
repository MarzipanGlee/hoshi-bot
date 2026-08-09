namespace HoshiBot.Domain.Entities;

public enum BoardingStatus
{
    // Boarding role granted, waiting for the member to confirm.
    Boarded,

    // Boarded, but the welcome DM could not be delivered (DMs closed). Not a failure worth retrying:
    // the standing message is still there and still works.
    Undeliverable,

    // Member role granted, boarding role removed, DM cleaned up.
    Confirmed,

    // They clicked, and the bot could not grant the member role — almost always a role sitting above
    // the bot's own. The boarding role deliberately stays, so the member still reads as not-done,
    // and the sync job retries once an admin fixes the hierarchy.
    RoleGrantFailed,
}
