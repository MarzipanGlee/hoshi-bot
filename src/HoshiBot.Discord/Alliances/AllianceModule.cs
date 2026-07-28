using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord.Alliances;

public class AllianceModule(HoshiBotDbContext db, GuildFeatureService featureService, EmbedBranding embedBranding) : ApplicationCommandModule<ApplicationCommandContext>
{
    // Catalog-rendered strings (the feature-disabled guard) are pinned to German until
    // sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    [SlashCommand("set-diplomacy", "Set one of this guild's alliances' diplomatic status toward another alliance",
        DefaultGuildPermissions = Permissions.ManageGuild, Contexts = [InteractionContextType.Guild])]
    public Task SetDiplomacy(string ourAllianceTag, string targetAllianceTag, DiplomacyStatus status) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var guildId = Context.Guild!.Id;

            var ourGuildAlliance = await db.GuildAlliances
                .Include(ga => ga.StfcAlliance)
                .FirstOrDefaultAsync(ga => ga.GuildId == guildId && ga.StfcAlliance.Tag == ourAllianceTag);
            if (ourGuildAlliance is null)
                return $"This guild doesn't manage an alliance tagged \"{ourAllianceTag}\". Ask an admin to link it via the web admin.";

            // Gate per that specific alliance — Diplomacy can be enabled for one linked alliance
            // but not another.
            if (!await featureService.IsEnabledAsync(guildId, GuildFeature.Diplomacy, GuildAudience.Alliance, ourGuildAlliance.Id))
                return GuildFeatureService.DisabledMessage(GuildFeature.Diplomacy, Lang);

            var ourAlliance = ourGuildAlliance.StfcAlliance;

            // Scoped to our own alliance's server — diplomacy is always within the same
            // server in STFC, and Tag alone isn't globally unique across the full catalog.
            var targetAlliance = await db.StfcAlliances.FirstOrDefaultAsync(a =>
                a.Tag == targetAllianceTag && a.ServerId == ourAlliance.ServerId);
            if (targetAlliance is null)
                return $"No alliance with tag \"{targetAllianceTag}\" found. Ask an admin to add it first (via the web admin).";

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

            return $"Set {ourAlliance.Tag}'s diplomatic status toward {targetAlliance.Name} ({targetAlliance.Tag}) to **{status}**.";
        });
}
