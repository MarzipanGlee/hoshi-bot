using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using HoshiBot.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NetCord.Rest;

namespace HoshiBot.Web.Authorization;

// Two-tier permission check, mirroring YAGPDB's web dashboard:
//   1. Discord-native: the guild owner, or anyone with the "Manage Server" permission,
//      per the *user's own* OAuth token (GET /users/@me/guilds).
//   2. Allow-listed role fallback: if the guild has GuildAdminRole entries, grant access
//      to members holding one of those roles, looked up via the *bot's* token.
public class GuildAdminHandler(
    IHttpContextAccessor httpContextAccessor,
    IHttpClientFactory httpClientFactory,
    HoshiBotDbContext db,
    IMemoryCache cache,
    RestClient botRestClient) : AuthorizationHandler<GuildAdminRequirement, ulong>
{
    private const ulong ManageGuildPermission = 0x20;

    private static readonly JsonSerializerOptions UserGuildsJsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

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

        var userGuilds = await cache.GetOrCreateAsync($"discord-user-guilds:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);

            var client = httpClientFactory.CreateClient("DiscordUserApi");
            using var request = new HttpRequestMessage(HttpMethod.Get, "users/@me/guilds");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<List<DiscordUserGuild>>(UserGuildsJsonOptions);
        });

        var userGuild = userGuilds?.FirstOrDefault(g => g.Id == guildId);
        if (userGuild is not null && (userGuild.Owner || (userGuild.Permissions & ManageGuildPermission) != 0))
        {
            context.Succeed(requirement);
            return;
        }

        var allowedRoleIds = await db.GuildAdminRoles
            .Where(r => r.GuildId == guildId)
            .Select(r => r.DiscordRoleId)
            .ToListAsync();

        if (allowedRoleIds.Count == 0)
            return;

        var memberRoleIds = await cache.GetOrCreateAsync($"discord-member-roles:{guildId}:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            var member = await botRestClient.GetGuildUserAsync(guildId, userId);
            return member.RoleIds;
        });

        if (memberRoleIds is not null && memberRoleIds.Any(allowedRoleIds.Contains))
        {
            context.Succeed(requirement);
        }
    }
}
