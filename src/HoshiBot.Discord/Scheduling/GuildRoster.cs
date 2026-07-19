using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.Scheduling;

// Shared helper for the per-member sync jobs (rank/ops-level roles, nickname): fetch the whole guild
// roster ONCE via the paginated GetGuildUsersAsync (one bulk request that already carries each
// member's roles + nickname) instead of a GetGuildUserAsync per member — the latter fans out into
// hundreds of individual REST calls that trip Discord's per-route user rate limit on larger guilds.
internal static class GuildRoster
{
    public static async Task<Dictionary<ulong, GuildUser>> FetchAsync(GatewayClient client, ulong guildId, CancellationToken cancellationToken = default)
    {
        var map = new Dictionary<ulong, GuildUser>();
        await foreach (var user in client.Rest.GetGuildUsersAsync(guildId).WithCancellation(cancellationToken))
            map[user.Id] = user;
        return map;
    }
}
