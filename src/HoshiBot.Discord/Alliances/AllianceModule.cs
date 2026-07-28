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
    // All strings come from the message catalog (Msg.Alliance); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
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
                return Msg.Alliance.NotManagedHere(Lang, ourAllianceTag);

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
                return Msg.Alliance.TargetNotFound(Lang, targetAllianceTag);

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

            return Msg.Alliance.DiplomacySet(Lang, ourAlliance.Tag, targetAlliance.Name, targetAlliance.Tag, status.ToString());
        });
}
