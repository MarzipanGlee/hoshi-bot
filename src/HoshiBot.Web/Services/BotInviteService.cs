using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace HoshiBot.Web.Services;

// Builds Discord's bot-authorization URL, and works out what a specific guild actually needs.
//
// The invite used to ask every guild for the same eleven permissions. Now /invite asks only for
// GuildFeaturePermissions.InviteBaseline — enough for the Setup Wizard and for posting at all — and
// anything a feature needs on top arrives through a per-guild re-authorize link, computed from the
// features that guild has actually switched on. A guild that never runs Nickname Sync is never
// asked for Manage Nicknames.
//
// There is deliberately no /reauthorize endpoint: the link is rendered on the Permission Check
// page, which is already guild-admin gated, so this avoids a second authorization surface for a
// URL that has to be built per guild anyway.
public class BotInviteService(GuildFeatureService featureService, IConfiguration configuration)
{
    // Baseline plus everything this guild's enabled features declare. Channel-profile bits are
    // included on purpose: granting them on the bot's role covers every channel at once, which is
    // the whole point of offering a top-up instead of making an admin fix twenty channels by hand.
    public async Task<BotPermission> NeededForAsync(ulong guildId)
    {
        var needed = GuildFeaturePermissions.InviteBaseline;

        foreach (var (feature, _, _) in await featureService.GetEnabledAsync(guildId))
        {
            needed |= GuildFeaturePermissions.GuildPermissions(feature);
            foreach (var slot in GuildFeaturePermissions.ChannelSlots(feature))
                needed |= slot.Profile.Permissions();
        }

        // The AiChatKnowledge* tiers are never in GuildEnabledFeature (they ride AiChat's
        // enablement), but their Read profile is View + Read Message History, both already in the
        // baseline — so there is nothing to add for them here.
        return needed;
    }

    // Discord's authorize flow REPLACES the bot's managed-role permissions with exactly what is
    // ticked, so this always requests needed | current. Requesting only what we calculate would
    // silently strip a permission an admin granted deliberately — which is also why there is no
    // "trim it down" button anywhere.
    public string AuthorizeUrl(ulong guildId, BotPermission needed, BotPermission current)
    {
        var clientId = configuration["Discord:ClientId"];
        var permissions = (ulong)(needed | current);
        return $"https://discord.com/oauth2/authorize?client_id={clientId}&permissions={permissions}"
            + $"&scope=bot%20applications.commands&guild_id={guildId}&disable_guild_select=true";
    }
}
