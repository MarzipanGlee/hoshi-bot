using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord;

// Self-service beta-test opt-in/out behind the staff bridge's "Beta-Tests verwalten" button:
// toggles the caller's own GuildSettings.BetaTesterRoleId role. Infra (RestClient/settings)
// lives here rather than in the interaction module, per the repo's service-layer convention.
public class BetaTesterService(HoshiBotDbContext db, GatewayClient gatewayClient, NotificationDispatcher dispatcher, LanguageResolver languageResolver)
{
    private async Task<ulong?> GetRoleIdAsync(ulong guildId) =>
        await db.GuildSettings.Where(s => s.GuildId == guildId).Select(s => s.BetaTesterRoleId).FirstOrDefaultAsync();

    // (configured, hasRole) — configured is false when no beta role is set for the guild.
    public async Task<(bool Configured, bool HasRole)> GetStatusAsync(ulong guildId, ulong userId)
    {
        if (await GetRoleIdAsync(guildId) is not { } roleId)
            return (false, false);

        var guildUser = await gatewayClient.Rest.GetGuildUserAsync(guildId, userId);
        return (true, guildUser.RoleIds.Contains(roleId));
    }

    // callerLanguage: the acting staff member's resolved language — the status strings go back
    // ephemerally to whoever clicked (the module resolves once for prompt + result); the admin
    // notification on failure uses the guild language instead.
    public async Task<string> SetAsync(ulong guildId, ulong userId, bool on, Language callerLanguage)
    {
        if (await GetRoleIdAsync(guildId) is not { } roleId)
            return Msg.Bridge.BetaRoleNotConfigured(callerLanguage);

        try
        {
            if (on)
                await gatewayClient.Rest.AddGuildUserRoleAsync(guildId, userId, roleId);
            else
                await gatewayClient.Rest.RemoveGuildUserRoleAsync(guildId, userId, roleId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            var guildLanguage = await languageResolver.ForGuildAsync(guildId);
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, BotAction.AdjustBetaTesterRole, null, BotPermission.ManageRoles);
            return Msg.Bridge.BetaToggleFailed(callerLanguage);
        }

        return on ? Msg.Bridge.BetaEnabled(callerLanguage) : Msg.Bridge.BetaDisabled(callerLanguage);
    }
}
