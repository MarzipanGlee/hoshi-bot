using HoshiBot.Data;
using HoshiBot.Discord.Absences;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Sweeps abandoned create/edit drafts, then refreshes both persistent Absences report
// messages for every guild that has one configured — this is what moves a Confirmed
// absence between "Kommende"/"Aktuelle"/gone purely from time passing, with no user action.
public class AbsenceReportRefreshJob(HoshiBotDbContext db, AbsenceService absenceService, GuildFeatureService featureService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await absenceService.SweepStaleDraftsAsync();

        var guildIds = await db.GuildSettings
            .Where(s => s.AbsencesReportChannelId != null || s.AbsencesReportStaffChannelId != null)
            .Select(s => s.GuildId)
            .ToListAsync();

        foreach (var guildId in guildIds)
        {
            if (!await featureService.IsEnabledAsync(guildId, GuildFeature.Absences))
                continue;

            await absenceService.RefreshReportsAsync(guildId);
        }
    }
}
