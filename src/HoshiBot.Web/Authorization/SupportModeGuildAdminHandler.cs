using System.Security.Claims;
using HoshiBot.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Authorization;

// A second handler for GuildAdminRequirement, additive to GuildAdminHandler (ASP.NET runs
// every registered handler and OR's their results): grants access to ANY guild when the
// user is a global admin who has support mode switched on. This is what lets a support
// admin open a guild page they don't personally administer, including via direct URL /
// refresh — GuildAdminPageBase's AuthorizeAsync(user, guildId, GuildAdminRequirement())
// works unmodified.
//
// Reads the DB directly (mirrors GlobalAdminHandler) and takes no IAuthorizationService /
// GuildAccessService dependency — an IAuthorizationHandler can never depend on those (see
// GuildAdminHandler / DiscordUserGuildsService doc comments for the circular-dependency
// reason).
public class SupportModeGuildAdminHandler(IDbContextFactory<HoshiBotDbContext> dbFactory)
    : AuthorizationHandler<GuildAdminRequirement, ulong>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, GuildAdminRequirement requirement, ulong guildId)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !ulong.TryParse(userIdClaim, out var userId))
            return;

        await using var db = await dbFactory.CreateDbContextAsync();
        var admin = await db.GlobalAdmins.FindAsync(userId);
        if (admin is { SupportMode: true })
        {
            context.Succeed(requirement);
        }
    }
}
