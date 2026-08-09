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

    // The role pinged in this channel — for ServerStatus/InfiniteIncursions/AllianceTournament,
    // which genuinely differ per channel. NULL for Raid/Shield rows, which ping their alliance's
    // GuildAlliance.AlertRoleId instead: those two are what members opt into, so the pinged role and
    // the opt-in role have to be the same one.
    public ulong? RoleId { get; set; }

    // Which linked alliance this row serves, for Alliance-audience rows. Raid and shield channels
    // need it to resolve whose alert role to ping; it also fixes the language edge noted in
    // NotificationDispatcher, where an Alliance row fell back to the guild language because it could
    // not say which alliance it belonged to. Null for the non-alliance audiences.
    public int? GuildAllianceId { get; set; }

    public GuildAlliance? GuildAlliance { get; set; }

    // Which audience this specific channel serves — RaidAlerts/ShieldReminders rows are
    // always Alliance (their only relevant audience); ServerStatus/InfiniteIncursions rows are a
    // genuine per-row admin choice.
    public GuildAudience Audience { get; set; }
}
