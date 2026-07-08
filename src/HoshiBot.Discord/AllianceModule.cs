using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord;

public class AllianceModule(HoshiBotDbContext db, GuildFeatureService featureService) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("set-diplomacy", "Set one of this guild's alliances' diplomatic status toward another alliance",
        DefaultGuildPermissions = Permissions.ManageGuild, Contexts = [InteractionContextType.Guild])]
    public async Task<InteractionMessageProperties> SetDiplomacy(string ourAllianceTag, string targetAllianceTag, DiplomacyStatus status)
    {
        var guildId = Context.Guild!.Id;

        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.Diplomacy))
            return EphemeralReply.Of(GuildFeatureService.DisabledMessage(GuildFeature.Diplomacy));

        var ourAlliance = await db.GuildAlliances
            .Include(ga => ga.StfcAlliance)
            .Where(ga => ga.GuildId == guildId && ga.StfcAlliance.Tag == ourAllianceTag)
            .Select(ga => ga.StfcAlliance)
            .FirstOrDefaultAsync();
        if (ourAlliance is null)
            return EphemeralReply.Of($"This guild doesn't manage an alliance tagged \"{ourAllianceTag}\". Ask an admin to link it via the web admin.");

        // Scoped to our own alliance's server — diplomacy is always within the same
        // server in STFC, and Tag alone isn't globally unique across the full catalog.
        var targetAlliance = await db.StfcAlliances.FirstOrDefaultAsync(a =>
            a.Tag == targetAllianceTag && a.ServerId == ourAlliance.ServerId);
        if (targetAlliance is null)
            return EphemeralReply.Of($"No alliance with tag \"{targetAllianceTag}\" found. Ask an admin to add it first (via the web admin).");

        var diplomacy = await db.StfcAllianceDiplomacies.FirstOrDefaultAsync(d =>
            d.SourceAllianceId == ourAlliance.Id && d.TargetAllianceId == targetAlliance.Id);
        if (diplomacy is null)
        {
            diplomacy = new StfcAllianceDiplomacy
            {
                SourceAllianceId = ourAlliance.Id,
                TargetAllianceId = targetAlliance.Id,
                Status = status,
            };
            db.StfcAllianceDiplomacies.Add(diplomacy);
        }
        else
        {
            diplomacy.Status = status;
        }

        await db.SaveChangesAsync();

        return EphemeralReply.Of($"Set {ourAlliance.Tag}'s diplomatic status toward {targetAlliance.Name} ({targetAlliance.Tag}) to **{status}**.");
    }
}
