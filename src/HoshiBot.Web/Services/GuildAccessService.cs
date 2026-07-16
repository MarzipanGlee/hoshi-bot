using System.Security.Claims;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Services;

// Consolidates the per-guild authorize loop that was previously duplicated across the
// Dashboard's guild-picker and the old standalone guild-picker page (now merged into
// Manage/Index.razor). Depends on IAuthorizationService, so this is for PAGES only — never
// inject this into an IAuthorizationHandler (see DiscordUserGuildsService's doc comment for
// why that's a circular dependency).
public class GuildAccessService(
    IAuthorizationService authorizationService,
    IDbContextFactory<HoshiBotDbContext> dbFactory,
    SupportModeContext supportMode)
{
    // Guilds our bot already knows about (has a DiscordGuild row) that the current user
    // can administer — existing Manage/Index.razor (Dashboard) behavior, consolidated.
    public async Task<List<DiscordGuild>> GetAccessibleGuildsAsync(ClaimsPrincipal user)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var allGuilds = await db.DiscordGuilds.AsNoTracking().ToListAsync();

        // Support mode (only ever active for a global admin) grants access to every bot
        // guild. Short-circuit the per-guild AuthorizeAsync loop below — it's not just
        // faster, it also avoids firing GuildAdminHandler's per-guild Discord/bot REST
        // calls across the entire guild list.
        if (supportMode.IsActive)
            return allGuilds;

        var accessible = new List<DiscordGuild>();
        foreach (var guild in allGuilds)
        {
            var result = await authorizationService.AuthorizeAsync(user, guild.Id, new GuildAdminRequirement());
            if (result.Succeeded)
                accessible.Add(guild);
        }

        return accessible;
    }
}
