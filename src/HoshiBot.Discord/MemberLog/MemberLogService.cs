using HoshiBot.Data;
using HoshiBot.Discord.Permissions;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.MemberLog;

// Posts a line to the guild's log channel whenever a member joins or leaves — a straight port of the
// legacy bot's join-message.yag / leave-message.yag, which wrote to the same
// GuildSettings.UserLogChannelId this reads. The column and its picker survived the rewrite; the
// feature didn't, so three of five guilds had a log channel configured that silently did nothing.
//
// No feature toggle: the channel IS the toggle, matching how the guild-wide Log and Admin channels
// already work. Configure it and the log appears; clear it and it stops.
//
// Both usernames are recorded because Discord has two and they diverge: the global display name a
// member chose, and the unique @handle. When someone leaves, the handle is often the only thing that
// still identifies them — the member row is gone from the guild by the time this fires.
public class MemberLogService(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    EmbedBranding embedBranding,
    ChannelCooldown cooldown,
    LanguageResolver languageResolver,
    ILogger<MemberLogService> logger)
{
    public Task LogJoinAsync(ulong guildId, User user) =>
        LogAsync(guildId, user, joined: true);

    public Task LogLeaveAsync(ulong guildId, User user) =>
        LogAsync(guildId, user, joined: false);

    private async Task LogAsync(ulong guildId, User user, bool joined)
    {
        // Bots joining is noise — the interesting event is a person arriving or going.
        if (user.IsBot)
            return;

        var settings = await db.GuildSettings.FirstOrDefaultAsync(s => s.GuildId == guildId);
        if (settings?.UserLogChannelId is not { } channelId)
            return;

        if (cooldown.IsCoolingDown(channelId, BotAction.SendAlert))
            return;

        // The log channel is staff-facing and guild-wide, so it renders in the guild's language.
        var lang = await languageResolver.ForGuildAsync(guildId);

        var description = joined
            ? Msg.MemberLog.Joined(lang, $"<@{user.Id}>")
            : Msg.MemberLog.Left(lang, $"<@{user.Id}>");

        var embed = await embedBranding.BuildBrandedAsync(guildId, description,
            joined ? EmbedBranding.SuccessColor : EmbedBranding.DangerColor);

        embed.Fields =
        [
            new EmbedFieldProperties { Name = Msg.MemberLog.FieldUserId(lang), Value = user.Id.ToString() },
            new EmbedFieldProperties { Name = Msg.MemberLog.FieldGlobalName(lang), Value = Fallback(user.GlobalName) },
            new EmbedFieldProperties { Name = Msg.MemberLog.FieldUsername(lang), Value = Fallback(user.Username) },
        ];

        try
        {
            // Never let a display name or a mention in it ping anyone — this is a record, not a
            // notification.
            await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties
            {
                Embeds = [embed],
                AllowedMentions = AllowedMentionsProperties.None,
            });
            cooldown.RecordSuccess(channelId, BotAction.SendAlert);
        }
        catch (RestException ex)
        {
            cooldown.RecordFailure(channelId, BotAction.SendAlert);
            logger.LogWarning(ex, "Could not write the member {Event} entry to log channel {ChannelId} for guild {GuildId}",
                joined ? "join" : "leave", channelId, guildId);
        }
    }

    // Legacy rendered a missing name as "-" rather than an empty field, and an empty embed field
    // value is rejected by Discord outright.
    private static string Fallback(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;
}
