using System.Net;
using System.Security.Claims;
using HoshiBot.Data;
using HoshiBot.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NetCord.Rest;

namespace HoshiBot.Web.Authorization;

// Two-tier permission check, mirroring YAGPDB's web dashboard:
//   1. Discord-native: the guild owner, or anyone with the "Manage Server" permission,
//      per the *user's own* OAuth token (GET /users/@me/guilds, via DiscordUserGuildsService).
//   2. Allow-listed role fallback: if the guild has GuildAdminRole entries, grant access
//      to members holding one of those roles, looked up via the *bot's* token.
//
// Deliberately depends on DiscordUserGuildsService, NOT GuildAccessService — the latter
// needs IAuthorizationService, and an IAuthorizationHandler can never depend on that
// (even transitively): building IAuthorizationService enumerates every registered
// handler, so a handler needing it back is a circular dependency by construction.
public class GuildAdminHandler(
    IHttpContextAccessor httpContextAccessor,
    DiscordUserGuildsService discordUserGuildsService,
    IDbContextFactory<HoshiBotDbContext> dbFactory,
    IMemoryCache cache,
    RestClient botRestClient) : AuthorizationHandler<GuildAdminRequirement, ulong>
{
    private const ulong ManageGuildPermission = 0x20;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, GuildAdminRequirement requirement, ulong guildId)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return;

        var accessToken = await httpContext.GetTokenAsync("access_token");
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (accessToken is null || userIdClaim is null || !ulong.TryParse(userIdClaim, out var userId))
            return;

        var userGuilds = await discordUserGuildsService.GetUserDiscordGuildsAsync(accessToken, userId);
        var userGuild = userGuilds.FirstOrDefault(g => g.Id == guildId);
        if (userGuild is not null && (userGuild.Owner || (userGuild.Permissions & ManageGuildPermission) != 0))
        {
            context.Succeed(requirement);
            return;
        }

        var allowedRoleIds = await cache.GetOrCreateAsync($"discord-guild-admin-roles:{guildId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            await using var db = await dbFactory.CreateDbContextAsync();
            return await db.GuildAdminRoles
                .Where(r => r.GuildId == guildId)
                .Select(r => r.DiscordRoleId)
                .ToListAsync();
        });

        if (allowedRoleIds is null || allowedRoleIds.Count == 0)
            return;

        var memberRoleIds = await cache.GetOrCreateAsync($"discord-member-roles:{guildId}:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            try
            {
                var member = await botRestClient.GetGuildUserAsync(guildId, userId);
                return member.RoleIds;
            }
            catch (RestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            {
                // The user simply isn't a member of this guild (404 "Unknown Member"), or the bot
                // can't see them (403) — either way they hold none of its admin roles. Return "no
                // roles" instead of letting the exception bubble up: GuildAccessService authorizes
                // the user against EVERY bot guild, and they aren't a member of most of them, so an
                // unhandled 404 here broke the whole Manage/Dashboard page.
                return [];
            }
        });

        if (memberRoleIds is not null && memberRoleIds.Any(allowedRoleIds.Contains))
        {
            context.Succeed(requirement);
        }
    }
}
