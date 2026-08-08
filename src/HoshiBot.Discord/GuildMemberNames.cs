using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord;

// Resolves a member's display name — the tag-stripped nickname the rest of the bot addresses people
// by — for a User that arrived without member data.
//
// Why this exists: Discord attaches the member object, and therefore the nickname, to a message
// delivered over the GATEWAY but not to one fetched over REST. NetCord reflects that faithfully, so
// the same person reads as "MarzipanGlee" live and as their global name when the message is fetched
// back. That is how a published announcement greeted its own author as "Commander Oops".
//
// The gateway's member cache is NOT a fix for it: NetCord fills that from GUILD_MEMBER_ADD/UPDATE
// and from GUILD_CREATE, and Discord omits members from GUILD_CREATE for any guild past its large
// threshold — a 956-member guild starts with an effectively empty one and message authors are never
// added to it. Reading it and hoping looks like it works and silently doesn't.
//
// So: cache, then REST, then remember the answer. Memoized per (guild, member) with a TTL, which is
// what makes the bulk callers affordable — indexing 300 messages from one channel is a handful of
// distinct authors, not 300 lookups. Failures are memoized too, briefly, so a member who has left
// doesn't cost a request per message they ever wrote.
public class GuildMemberNames(GatewayClient gatewayClient, ILogger<GuildMemberNames> logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    // Short, because the fallback is a worse name rather than a wrong one — worth retrying sooner
    // than a good answer needs refreshing.
    private static readonly TimeSpan FailureTtl = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), (string Name, DateTimeOffset Until)> _cache = new();

    public async ValueTask<string> ResolveAsync(ulong guildId, User user, CancellationToken cancellationToken = default)
    {
        // Already carries the nickname (a gateway message author, or an interaction user) — nothing
        // to look up, and worth recording for the REST-fetched copies of the same person.
        if (user is GuildUser member)
            return Remember(guildId, user.Id, CommanderName.Of(member), Ttl);

        var key = (guildId, user.Id);
        if (_cache.TryGetValue(key, out var cached) && cached.Until > DateTimeOffset.UtcNow)
            return cached.Name;

        if (gatewayClient.Cache.Guilds.TryGetValue(guildId, out var guild) && guild.Users.TryGetValue(user.Id, out var cachedMember))
            return Remember(guildId, user.Id, CommanderName.Of(cachedMember), Ttl);

        try
        {
            var fetched = await gatewayClient.Rest.GetGuildUserAsync(guildId, user.Id, cancellationToken: cancellationToken);
            return Remember(guildId, user.Id, CommanderName.Of(fetched), Ttl);
        }
        catch (RestException ex)
        {
            // They left, or the call failed. The global name is a worse greeting, not a wrong one,
            // so this degrades rather than throwing — but it is remembered so the next hundred
            // messages by the same person don't each cost a failed request.
            logger.LogDebug(ex, "Could not resolve member {UserId} in guild {GuildId}; falling back to their global name", user.Id, guildId);
            return Remember(guildId, user.Id, CommanderName.Of(user), FailureTtl);
        }
    }

    // Same resolution from an id alone, for callers holding a stored user id rather than a User —
    // the raid alert's reporter and terminator, say. The memo means the common case (the same few
    // people in one guild) costs nothing.
    public async ValueTask<string> ResolveNameAsync(ulong guildId, ulong userId, CancellationToken cancellationToken = default)
    {
        var key = (guildId, userId);
        if (_cache.TryGetValue(key, out var cached) && cached.Until > DateTimeOffset.UtcNow)
            return cached.Name;

        if (gatewayClient.Cache.Guilds.TryGetValue(guildId, out var guild) && guild.Users.TryGetValue(userId, out var cachedMember))
            return Remember(guildId, userId, CommanderName.Of(cachedMember), Ttl);

        try
        {
            return Remember(guildId, userId, CommanderName.Of(await gatewayClient.Rest.GetGuildUserAsync(guildId, userId, cancellationToken: cancellationToken)), Ttl);
        }
        catch (RestException ex)
        {
            logger.LogDebug(ex, "Could not resolve member {UserId} in guild {GuildId}", userId, guildId);
            return Remember(guildId, userId, userId.ToString(), FailureTtl);
        }
    }

    private string Remember(ulong guildId, ulong userId, string name, TimeSpan ttl)
    {
        _cache[(guildId, userId)] = (name, DateTimeOffset.UtcNow + ttl);
        return name;
    }
}
