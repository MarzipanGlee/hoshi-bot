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
        GuildFeature.MemberLore => GuildAudience.Alliance,
        // Guild-wide: player↔member assignment spans every alliance/server a guild's members belong
        // to, so it's a single guild-level toggle (Guild audience), not per-alliance.
        GuildFeature.PlayerLink => GuildAudience.Guild,
        GuildFeature.MemberOnboarding => GuildAudience.Community,
        GuildFeature.ShieldReminders => GuildAudience.Alliance,
        GuildFeature.TerritoryCapture => GuildAudience.Alliance,
        GuildFeature.RoeViolationReports => GuildAudience.Alliance,
        GuildFeature.AlertsOptIn => GuildAudience.Alliance,
        GuildFeature.Diplomacy => GuildAudience.Alliance,
        GuildFeature.RaidAlerts => GuildAudience.Alliance,
        GuildFeature.ServerStatus => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup,
        GuildFeature.InfiniteIncursions => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup,
        GuildFeature.Announcements => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup | GuildAudience.Community,
        GuildFeature.Tickets => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup | GuildAudience.Community,
        GuildFeature.AnonymousMessaging => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup | GuildAudience.Community,
        // Guild-wide role automation: one set of rank/ops-level roles for the whole Discord,
        // driven by imported player data — not per-audience or per-alliance. Single Guild toggle.
        GuildFeature.RankRoles => GuildAudience.Guild,
        GuildFeature.OpsLevelRoles => GuildAudience.Guild,
        // Guild-wide nickname sync, sibling of the two above (driven by the same player links).
        GuildFeature.NicknameSync => GuildAudience.Guild,
        GuildFeature.AllianceTournament => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup,
        // Single-audience on purpose, unlike its sibling notify features above: StfcNews has
        // no real per-audience distinction (it's one guild-wide toggle reusing the existing
        // AdminChannelId/CommandStaffRoleId settings, not per-audience alert channels), so
        // mapping it to multiple audiences would only force awkward multi-page routing
        // (FeatureSettings.razor requires an Audience param and renders one sub-page per
        // audience for any multi-audience feature) with no actual behavioral difference
        // between those pages.
        GuildFeature.StfcNews => GuildAudience.Alliance,
        GuildFeature.ClientRelease => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup,
        // AI chat is available under any audience — its listen/knowledge channel lists and its
        // per-audience behavioral settings (system prompt, search language, memory toggle,
        // streaming) are per-audience (like Announcements/Tickets). The guild-wide backend scalars
        // (provider, API key, models, embeddings) live in the separate AiBackend feature below.
        // AiChatKnowledge mirrors the same audiences so its channel bucket lines up with AiChat's
        // per audience.
        GuildFeature.AiChat => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup | GuildAudience.Community,
        GuildFeature.AiChatKnowledge => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup | GuildAudience.Community,
        // Preferred/LastResort knowledge tiers mirror AiChatKnowledge's audiences (same channel buckets).
        GuildFeature.AiChatKnowledgePreferred => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup | GuildAudience.Community,
        GuildFeature.AiChatKnowledgeLastResort => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup | GuildAudience.Community,
        // Per-alliance hub messages — one set of bridges per linked alliance (channels live on
        // GuildAlliance). Single Alliance audience.
        GuildFeature.CommandBridge => GuildAudience.Alliance,
        // Guild-wide, single toggle: Scopely's official announcements aren't alliance-specific, so
        // one destination channel/source-channel set for the whole Discord, not per audience.
        // Per-audience since the destinations are: a coalition guild forwards the same source
        // announcement into each alliance's own channel, in each alliance's own language. Source
        // channels stay shared (GuildFeatureChannel carries no alliance id); only where a forward
        // LANDS is scoped.
        GuildFeature.AnnouncementForwarder => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup | GuildAudience.Community,
        // Guild-wide AI backend/credentials/models shared by every AI feature — one account per
        // guild, so a single Guild-audience toggle (like NicknameSync/AnnouncementForwarder).
        GuildFeature.AiBackend => GuildAudience.Guild,
        // Per-alliance: it assigns the alliance's Services role gated on the alliance member role, so
        // it toggles per linked alliance (like TerritoryCapture, whose Services role it mirrors).
        GuildFeature.ServicesRoleSync => GuildAudience.Alliance,
        // Guild-wide admin utility (one /hoshi-say command + one allowed role for the whole Discord),
        // like AnnouncementForwarder/AiBackend — a single Guild-audience toggle.
        GuildFeature.HoshiSay => GuildAudience.Guild,
        // Per-alliance, like the TerritoryCapture posts it adds the sign-off buttons to and the
        // Absences feature whose rows those buttons write.
        GuildFeature.TerritoryCaptureSignOff => GuildAudience.Alliance,
        // Per-alliance: its own services channel/role and per-zone service curation, all scoped to
        // one linked alliance like the capture schedule it fires from.
        GuildFeature.TerritoryCaptureServiceReminders => GuildAudience.Alliance,
        // Guild-wide, like RankRoles/NicknameSync: one set of tag roles for the whole Discord, since
        // members' alliances span (and reach beyond) whatever the guild has linked.
        GuildFeature.AllianceTagRoles => GuildAudience.Guild,
        GuildFeature.ServerTagRoles => GuildAudience.Guild,
        GuildFeature.ConditionalRoles => GuildAudience.Guild,
        // Both ride on the user Command Bridge, which is per-alliance — and both point at channels
        // that differ per alliance, so they toggle per linked alliance like the other bridge buttons.
        GuildFeature.BotSupport => GuildAudience.Alliance,
        GuildFeature.ChannelGuide => GuildAudience.Alliance,
        // Per-audience like the kinds it gates: an alliance can want confirmations on its posts
        // while the community channel does not. Each post records the answer for ITS scope at the
        // moment it is made, so the matrix is only ever read once per post.
        GuildFeature.ReadReceipts => GuildAudience.Alliance | GuildAudience.Server | GuildAudience.VeilGroup | GuildAudience.Community,
        _ => GuildAudience.None,
    };

    public static bool HasMultipleAudiences(GuildFeature feature) =>
        EnumerateFlags(RelevantAudiences(feature)).Take(2).Count() > 1;

    // The one fixed audience for a single-audience feature. Throws for the 7 features with
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
