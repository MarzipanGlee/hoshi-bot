using HoshiBot.Discord.MemberLog;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace HoshiBot.Host;

// Member join/leave → the guild's log channel (MemberLogService). Ported from the legacy bot, whose
// join-message.yag / leave-message.yag wrote to the very same configured channel.
//
// Requires the GuildUsers (GUILD_MEMBERS) privileged intent — already enabled in Program.cs for the
// roster reads. Auto-registered by AddGatewayHandlers(typeof(Program).Assembly).
//
// GUILD_MEMBER_ADD carries a full GuildUser; GUILD_MEMBER_REMOVE carries only the plain User and the
// guild id, because by then the member no longer exists in the guild — which is exactly why the
// service records the ids and both usernames rather than a nickname.
public class MemberLogHandler(IServiceScopeFactory scopeFactory, ILogger<MemberLogHandler> logger)
    : IGuildUserAddGatewayHandler, IGuildUserRemoveGatewayHandler
{
    public ValueTask HandleAsync(GuildUser user) => LogAsync(user.GuildId, user, joined: true);

    public ValueTask HandleAsync(GuildUserRemoveEventArgs args) => LogAsync(args.GuildId, args.User, joined: false);

    private async ValueTask LogAsync(ulong guildId, User user, bool joined)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var memberLog = scope.ServiceProvider.GetRequiredService<MemberLogService>();
            if (joined)
                await memberLog.LogJoinAsync(guildId, user);
            else
                await memberLog.LogLeaveAsync(guildId, user);
        }
        catch (Exception ex)
        {
            // A logging feature must never take down the gateway handler pipeline for the event it
            // is only observing — MemberOnboardingHandler rides the same GUILD_MEMBER_ADD.
            logger.LogWarning(ex, "Member {Event} logging failed for guild {GuildId}", joined ? "join" : "leave", guildId);
        }
    }
}
