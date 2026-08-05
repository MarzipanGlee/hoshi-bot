namespace HoshiBot.Domain.Entities;

// One channel the bot is configured to use, resolved from a FeatureChannelSlot against a specific
// guild. Feature is null for the guild-wide slots (log / admin), which belong to no feature.
// ViaCategoryId is set when this row was produced by expanding a configured category into the child
// channels the bot actually works on.
public readonly record struct ChannelAccessRequirement(
    GuildFeature? Feature,
    ChannelSlotSource Source,
    string Key,
    GuildAudience Audience,
    int? GuildAllianceId,
    ulong ChannelId,
    ChannelAccessProfile Profile,
    bool CategoryExpands = false,
    ulong? ViaCategoryId = null);

// A requirement checked against what the bot actually has there. ChannelExists is false when the
// configured channel isn't in the guild's live channel list — deleted, or invisible to the bot,
// which amounts to the same thing from here.
public readonly record struct ChannelAccessFinding(
    ChannelAccessRequirement Requirement,
    BotPermission Effective,
    bool ChannelExists)
{
    public BotPermission Required => Requirement.Profile.Permissions();

    // A channel we can't see is missing everything, not nothing — otherwise a deleted channel would
    // read as "no permissions missing" and the row would look healthy.
    public BotPermission Missing => ChannelExists ? Required & ~Effective : Required;

    public bool Ok => ChannelExists && Missing == BotPermission.None;
}

public enum FeaturePermissionStatus
{
    /// Discord couldn't be reached — say so rather than showing a green badge built on nothing.
    Unknown,
    Ok,

    /// Something the bot needs is missing: a channel permission, or a guild-level one.
    Missing,

    /// A configured channel no longer exists (or the bot cannot see it at all).
    ChannelMissing,

    /// The feature declares channel slots but the guild has configured none of them. Not a
    /// permission problem — IsConfiguredAsync already owns that message — so callers generally
    /// render nothing for it.
    NoChannelsConfigured,
}

public readonly record struct FeaturePermissionSummary(
    GuildFeature Feature,
    GuildAudience Audience,
    int? GuildAllianceId,
    FeaturePermissionStatus Status,
    BotPermission MissingGuildPermissions,
    IReadOnlyList<ChannelAccessFinding> Findings);

/// One enabled (feature, audience, alliance) triple. Needed so a feature that is switched on but has
/// configured nothing can be told apart from one that isn't switched on at all.
public readonly record struct FeatureScope(GuildFeature Feature, GuildAudience Audience, int? GuildAllianceId);

// The decision half of the permission audit, kept pure so it can actually be tested: no EF, no
// NetCord, no Discord client. The caller supplies effective permissions as a lookup, which is the
// seam that keeps NetCord out of Domain — HoshiBot.Web passes a lambda over NetCord's own
// GuildUser.GetChannelPermissions, so the Administrator bypass and overwrite resolution stay in the
// one implementation that already handles them correctly.
public static class ChannelAccessEvaluator
{
    // effective returns null for a channel that isn't in the guild's live list.
    public static IReadOnlyList<ChannelAccessFinding> Evaluate(
        IReadOnlyList<ChannelAccessRequirement> requirements,
        Func<ulong, BotPermission?> effective) =>
        [.. requirements.Select(r => effective(r.ChannelId) is { } perms
            ? new ChannelAccessFinding(r, perms, ChannelExists: true)
            : new ChannelAccessFinding(r, BotPermission.None, ChannelExists: false))];

    // Groups findings for display, one summary per (feature, audience, alliance). The knowledge
    // tiers fold into AI Chat via DisplayOwner — they have no feature card of their own.
    //
    // enabledScopes exists so an enabled feature that has configured nothing still gets a summary
    // (NoChannelsConfigured) instead of silently vanishing from the report.
    public static IReadOnlyList<FeaturePermissionSummary> GroupByFeature(
        IReadOnlyList<ChannelAccessFinding> findings,
        BotPermission botGuildPermissions,
        IReadOnlyCollection<FeatureScope> enabledScopes)
    {
        var byScope = findings
            .Where(f => f.Requirement.Feature is not null)
            .GroupBy(f => new FeatureScope(
                GuildFeaturePermissions.DisplayOwner(f.Requirement.Feature!.Value),
                f.Requirement.Audience,
                f.Requirement.GuildAllianceId))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ChannelAccessFinding>)[.. g]);

        // Union so an enabled feature with nothing configured still appears, and a configured
        // channel whose feature isn't in enabledScopes (a stale row) isn't silently dropped.
        var scopes = new HashSet<FeatureScope>(enabledScopes);
        scopes.UnionWith(byScope.Keys);

        return
        [
            .. scopes
                .Select(scope =>
                {
                    var scopeFindings = byScope.TryGetValue(scope, out var found) ? found : [];
                    var missingGuild = GuildFeaturePermissions.GuildPermissions(scope.Feature) & ~botGuildPermissions;
                    return new FeaturePermissionSummary(
                        scope.Feature, scope.Audience, scope.GuildAllianceId,
                        Status(scope.Feature, scopeFindings, missingGuild),
                        missingGuild,
                        scopeFindings);
                })
                .OrderBy(s => s.Status is FeaturePermissionStatus.Ok or FeaturePermissionStatus.NoChannelsConfigured)
                .ThenBy(s => s.Feature),
        ];
    }

    // What the Fix button has to grant on a channel: the union of everything pointing at it. Two
    // features can share one channel with different profiles, and granting only the row the admin
    // happened to click would leave the other one failing on the very next re-check.
    public static IReadOnlyDictionary<ulong, BotPermission> RequiredByChannel(IReadOnlyList<ChannelAccessFinding> findings) =>
        findings
            .GroupBy(f => f.Requirement.ChannelId)
            .ToDictionary(g => g.Key, g => g.Aggregate(BotPermission.None, (acc, f) => acc | f.Required));

    // Missing outranks ChannelMissing when a feature has both: a missing permission is the one the
    // admin can act on from the page, whereas a deleted channel needs re-configuring elsewhere.
    private static FeaturePermissionStatus Status(
        GuildFeature feature, IReadOnlyList<ChannelAccessFinding> findings, BotPermission missingGuild)
    {
        if (missingGuild != BotPermission.None || findings.Any(f => f.ChannelExists && !f.Ok))
            return FeaturePermissionStatus.Missing;

        if (findings.Any(f => !f.ChannelExists))
            return FeaturePermissionStatus.ChannelMissing;

        return findings.Count == 0 && GuildFeaturePermissions.ChannelSlots(feature).Count > 0
            ? FeaturePermissionStatus.NoChannelsConfigured
            : FeaturePermissionStatus.Ok;
    }
}
