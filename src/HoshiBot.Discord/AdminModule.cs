using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord;

public class AdminModule(HoshiBotDbContext db) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("set-notification-role", "Set the role used for general notifications in this server",
        DefaultGuildPermissions = Permissions.ManageGuild, Contexts = [InteractionContextType.Guild])]
    public async Task<InteractionMessageProperties> SetNotificationRole(Role role)
    {
        var guildId = Context.Guild!.Id;

        var notificationRole = await db.NotificationRoles
            .FirstOrDefaultAsync(r => r.GuildId == guildId && r.Kind == NotificationRoleKind.General);

        if (notificationRole is null)
        {
            db.NotificationRoles.Add(new NotificationRole
            {
                GuildId = guildId,
                DiscordRoleId = role.Id,
                Kind = NotificationRoleKind.General,
            });
        }
        else
        {
            notificationRole.DiscordRoleId = role.Id;
        }

        await db.SaveChangesAsync();

        return EphemeralReply.Of($"Notification role set to {role.Name}. It will be kept in sync every 10 minutes based on active absences.");
    }
}
