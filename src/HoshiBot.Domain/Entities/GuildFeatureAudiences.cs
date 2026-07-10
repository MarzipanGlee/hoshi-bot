namespace HoshiBot.Domain.Entities;

// Single source of truth for which audience(s) a GuildFeature is relevant to — shared by
// GuildFeatureService (HoshiBot.Data, used from HoshiBot.Discord) and FeatureCatalog
// (HoshiBot.Web, which layers Title/Description/EditorComponentType on top of this same
// fact). Lives in Domain so both can reach it without a project reference cycle.
public static class GuildFeatureAudiences
{
    public static GuildAudience RelevantAudiences(GuildFeature feature) => feature switch
    {
        GuildFeature.Absences => GuildAudience.Alliance,
        GuildFeature.ShieldReminders => GuildAudience.Alliance,
        GuildFeature.TerritoryCapture => GuildAudience.Alliance,
        GuildFeature.RoeViolationReports => GuildAudience.Alliance,
        GuildFeature.AlertsOptIn => GuildAudience.Alliance,
        GuildFeature.Diplomacy => GuildAudience.Alliance,
        GuildFeature.RaidAlerts => GuildAudience.Alliance,
        GuildFeature.ServerStatus => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup,
        GuildFeature.Incursion => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup,
        GuildFeature.Announcements => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup | GuildAudience.Community,
        GuildFeature.Tickets => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup | GuildAudience.Community,
        GuildFeature.AnonymousMessaging => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup | GuildAudience.Community,
        _ => GuildAudience.None,
    };

    public static bool HasMultipleAudiences(GuildFeature feature) =>
        EnumerateFlags(RelevantAudiences(feature)).Take(2).Count() > 1;

    // The one fixed audience for a single-audience feature. Throws for the 5 features with
    // more than one relevant audience — those require an explicit audience from the
    // caller; there is no safe default to fall back to.
    public static GuildAudience SingleAudience(GuildFeature feature)
    {
        var relevant = RelevantAudiences(feature);
        if (HasMultipleAudiences(feature))
            throw new InvalidOperationException(
                $"{feature} has multiple relevant audiences ({relevant}); the caller must resolve and pass a specific one.");
        return relevant;
    }

    public static IEnumerable<GuildAudience> EnumerateFlags(GuildAudience audiences)
    {
        foreach (GuildAudience flag in Enum.GetValues<GuildAudience>())
        {
            if (flag != GuildAudience.None && audiences.HasFlag(flag))
                yield return flag;
        }
    }
}
