using System.Security.Claims;
using HoshiBot.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Authorization;

// Bot-wide admin check: is this Discord user in the GlobalAdmins table? Separate from
// GuildAdminHandler, which is scoped to a specific guild's Manage Server permission or
// allow-listed role. Purely a local DB lookup — no Discord API calls needed.
public class GlobalAdminHandler(IDbContextFactory<HoshiBotDbContext> dbFactory) : AuthorizationHandler<GlobalAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, GlobalAdminRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !ulong.TryParse(userIdClaim, out var userId))
            return;

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.GlobalAdmins.FindAsync(userId) is not null)
        {
            context.Succeed(requirement);
        }
    }
}
