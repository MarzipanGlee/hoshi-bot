namespace HoshiBot.Domain.Entities;

// Which channel/role pair to notify for each kind of alert, ported from hoshi-bot-yagpdb's
// Channels.Alerts/Channels.ShieldAlerts (each a list of channel+role pairs — a guild can
// have several, not just one, matching the legacy lists having 2 entries each).
public class GuildAlertChannel
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public GuildAlertChannelKind Kind { get; set; }

    public ulong ChannelId { get; set; }

    public ulong RoleId { get; set; }

    // Which audience this specific channel serves — RaidAlerts/ShieldReminders rows are
    // always Alliance (their only relevant audience); ServerStatus/Incursion rows are a
    // genuine per-row admin choice.
    public GuildAudience Audience { get; set; }
}
