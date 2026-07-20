namespace HoshiBot.Domain.Entities;

// A declared dependency of one feature on another: the required Feature plus an optional Note
// explaining any nuance (e.g. a "soft" dependency that other paths can also satisfy). There is
// deliberately no notion of depending on a *subset* of the required feature — every current
// relationship is whole-feature, so the free-text Note carries any subtlety instead.
public readonly record struct FeatureDependency(GuildFeature Feature, string? Note = null);

// Single source of truth for which GuildFeature(s) another feature needs to actually work —
// a sibling of GuildFeatureAudiences (same Domain home so both HoshiBot.Web and
// HoshiBot.Data/Discord can consult it without a project-reference cycle). Consumed by the Web
// Features page/settings page today to surface hints; enablement is never *blocked* on these.
public static class GuildFeatureDependencies
{
    public static IReadOnlyList<FeatureDependency> Of(GuildFeature feature) => feature switch
    {
        // Member Lore only does anything once AI Chat is on — the lore it collects exists to
        // ground AI Chat's answers.
        GuildFeature.MemberLore => [new(GuildFeature.AiChat)],

        // Member Onboarding builds directly on Player Assignment's matcher (it DMs the members
        // Player Assignment couldn't place automatically).
        GuildFeature.MemberOnboarding => [new(GuildFeature.PlayerLink)],

        // Rank/Ops roles and nickname sync all run off the member↔player links Player Assignment
        // creates. Soft: the links can also be made by hand, so this is a strong hint rather than a
        // hard requirement.
        GuildFeature.RankRoles => [new(GuildFeature.PlayerLink, "Player links can also be created by hand.")],
        GuildFeature.OpsLevelRoles => [new(GuildFeature.PlayerLink, "Player links can also be created by hand.")],
        GuildFeature.NicknameSync => [new(GuildFeature.PlayerLink, "Player links can also be created by hand.")],

        _ => [],
    };
}
