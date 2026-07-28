using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord;

// Self-service beta-test opt-in/out behind the staff bridge's "Beta-Tests verwalten" button:
// toggles the caller's own GuildSettings.BetaTesterRoleId role. Infra (RestClient/settings)
// lives here rather than in the interaction module, per the repo's service-layer convention.
public class BetaTesterService(HoshiBotDbContext db, GatewayClient gatewayClient, NotificationDispatcher dispatcher)
{
    // All strings come from the message catalog (Msg.Bridge); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

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

    public async Task<string> SetAsync(ulong guildId, ulong userId, bool on)
    {
        if (await GetRoleIdAsync(guildId) is not { } roleId)
            return Msg.Bridge.BetaRoleNotConfigured(Lang);

        try
        {
            if (on)
                await gatewayClient.Rest.AddGuildUserRoleAsync(guildId, userId, roleId);
            else
                await gatewayClient.Rest.RemoveGuildUserRoleAsync(guildId, userId, roleId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, Msg.Bridge.BetaActionAdjustRole(Lang), Msg.Bridge.BetaHintManageRoles(Lang));
            return Msg.Bridge.BetaToggleFailed(Lang);
        }

        return on ? Msg.Bridge.BetaEnabled(Lang) : Msg.Bridge.BetaDisabled(Lang);
    }
}
