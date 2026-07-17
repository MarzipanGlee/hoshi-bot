using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.RoeViolations;

public class RoeViolationModalModule(RoeViolationService roeViolationService) : ComponentInteractionModule<ModalInteractionContext>
{
    // Always reached via the roe-violation-report ephemeral wizard message (either
    // directly, or through roe-violation-other's UserMenu step), so ModifyMessage is
    // always safe here — never the public hub.
    [ComponentInteraction("roe-violation-modal")]
    public Task ReportRoeViolation(string branch, ulong otherUserId) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var values = TextInputValues();
            var otherTag = values.GetValueOrDefault("tag") ?? "-";
            var otherName = values.GetValueOrDefault("name") ?? "";
            var guildId = Context.Guild!.Id;
            var reporterId = Context.User.Id;
            var reporterName = CommanderName.Of(Context.User);

            EmbedProperties embed;
            switch (branch)
            {
                case "to":
                    {
                        var (ownTag, ownName) = await roeViolationService.ResolveIdentityAsync(reporterId, reporterName);
                        embed = await roeViolationService.CreateReportAsync(guildId, reporterId, reporterName,
                            attackerTag: otherTag, attackerName: otherName,
                            defenderTag: ownTag, defenderName: ownName,
                            attackerDiscordUserId: null, reporterIsVictim: true);
                        break;
                    }
                case "from":
                    {
                        var (ownTag, ownName) = await roeViolationService.ResolveIdentityAsync(reporterId, reporterName);
                        embed = await roeViolationService.CreateReportAsync(guildId, reporterId, reporterName,
                            attackerTag: ownTag, attackerName: ownName,
                            defenderTag: otherTag, defenderName: otherName,
                            attackerDiscordUserId: null, reporterIsVictim: false);
                        break;
                    }
                case "other":
                    {
                        var (attackerTag, attackerName) = await roeViolationService.ResolveIdentityAsync(otherUserId, $"Commander {otherUserId}");
                        embed = await roeViolationService.CreateReportAsync(guildId, reporterId, reporterName,
                            attackerTag, attackerName,
                            defenderTag: otherTag, defenderName: otherName,
                            attackerDiscordUserId: otherUserId, reporterIsVictim: false);
                        break;
                    }
                default:
                    embed = await roeViolationService.ResultEmbedAsync(guildId, "RoE-Verstoss", "Unbekannter Meldetyp.");
                    break;
            }

            return m => { m.Content = ""; m.Embeds = [embed]; m.Components = []; };
        });

    private Dictionary<string, string> TextInputValues() =>
        Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .ToDictionary(i => i.CustomId, i => i.Value);
}
