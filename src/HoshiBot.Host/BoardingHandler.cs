using HoshiBot.Data;
using HoshiBot.Discord.Boarding;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace HoshiBot.Host;

// Boards a member the moment they arrive, rather than up to a sync cycle later.
//
// Three events, each for a reason:
//
//   ADD — the fast path. For a server, veil-group or community scope this is all that is needed:
//   anyone who joins is claimed.
//
//   UPDATE — the Alliance scope's real trigger. Whether a member belongs to the alliance depends on
//   their linked player, which lands seconds to minutes after the join (PlayerLinkSyncJob, or the
//   MemberOnboarding DM they answer). At GUILD_MEMBER_ADD there is usually nothing to match on yet.
//   MemberOnboardingHandler rides this same event for the same reason.
//
//   REMOVE — someone left mid-boarding. Delete the welcome DM pointing at a message they can no
//   longer see, and close the entry. The row itself stays: if they come back, they were boarded
//   once already, and the boarding message is still there for them.
//
// Every handler swallows its own exceptions. Three handlers share GUILD_MEMBER_ADD, and one of them
// throwing must not stop the other two — MemberLogHandler documents the same rule.
public class BoardingHandler(IServiceScopeFactory scopeFactory, ILogger<BoardingHandler> logger)
    : IGuildUserAddGatewayHandler, IGuildUserUpdateGatewayHandler, IGuildUserRemoveGatewayHandler
{
    public ValueTask HandleAsync(GuildUser user) => BoardAsync(user);

    public async ValueTask HandleAsync(GuildUserRemoveEventArgs args)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();

            var entry = await db.BoardingEntries.FirstOrDefaultAsync(e =>
                e.GuildId == args.GuildId && e.DiscordUserId == args.User.Id && e.DmMessageId != null);
            if (entry is null)
                return;

            var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
            await dispatcher.DeleteDirectMessageAsync(args.User.Id, entry.DmMessageId!.Value);

            entry.DmMessageId = null;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not clean up boarding for departing member {UserId} in guild {GuildId}", args.User.Id, args.GuildId);
        }
    }

    private async ValueTask BoardAsync(GuildUser user)
    {
        if (user.IsBot)
            return;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();

            var enabled = (await db.GuildEnabledFeatures
                .Where(f => f.GuildId == user.GuildId && f.Feature == GuildFeature.Boarding)
                .Select(f => new { f.Audience, f.GuildAllianceId })
                .ToListAsync())
                .Select(f => new BoardingScopes.Scope(f.Audience, f.GuildAllianceId))
                .ToList();
            if (enabled.Count == 0)
                return;

            // Only consult the player tables when an Alliance scope could actually use the answer.
            int? allianceId = null;
            if (enabled.Any(s => s.Audience == GuildAudience.Alliance))
            {
                var alliances = scope.ServiceProvider.GetRequiredService<GuildAllianceService>();
                allianceId = (await alliances.FindByMemberAsync(user.GuildId, user.Id))?.Id;
            }

            if (BoardingScopes.Claim(enabled, allianceId) is not { } claimed)
                return;

            var boarding = scope.ServiceProvider.GetRequiredService<BoardingService>();
            await boarding.BoardAsync(user.GuildId, user, claimed, sendDm: true);
        }
        catch (Exception ex)
        {
            // Never rethrow: MemberLogHandler and MemberOnboardingHandler ride the same event.
            logger.LogWarning(ex, "Could not board member {UserId} in guild {GuildId}", user.Id, user.GuildId);
        }
    }
}
