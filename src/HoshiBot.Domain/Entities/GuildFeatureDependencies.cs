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
        // AI Chat needs the guild-wide AI backend (provider + API key + model) configured before it
        // can answer anything.
        GuildFeature.AiChat => [new(GuildFeature.AiBackend)],

        // Member Lore needs both: the AI backend to run its DM interviews + note extraction (via
        // AiChatModelResolver), and AI Chat itself — the lore it collects only does anything once
        // AI Chat injects it into answers.
        GuildFeature.MemberLore => [new(GuildFeature.AiBackend), new(GuildFeature.AiChat, "The collected lore is used to ground AI Chat's answers.")],

        // The forwarder's translation calls reuse the guild-wide AI backend model/API key
        // (AiChatModelResolver) — without it configured, it has no model to translate with.
        GuildFeature.AnnouncementForwarder => [new(GuildFeature.AiBackend, "Uses the AI backend's configured model to translate.")],

        // /hoshi-say composes its message with the guild-wide AI backend's model — without it
        // configured, there's nothing to compose the text with.
        GuildFeature.HoshiSay => [new(GuildFeature.AiBackend, "Hoshi composes the message with the AI backend's configured model.")],

        // Member Onboarding builds directly on Player Assignment's matcher (it DMs the members
        // Player Assignment couldn't place automatically).
        GuildFeature.MemberOnboarding => [new(GuildFeature.PlayerLink)],

        // Rank/Ops roles and nickname sync all run off the member↔player links Player Assignment
        // creates. Soft: the links can also be made by hand, so this is a strong hint rather than a
        // hard requirement.
        GuildFeature.RankRoles => [new(GuildFeature.PlayerLink, "Player links can also be created by hand.")],
        GuildFeature.OpsLevelRoles => [new(GuildFeature.PlayerLink, "Player links can also be created by hand.")],
        GuildFeature.NicknameSync => [new(GuildFeature.PlayerLink, "Player links can also be created by hand.")],

        // Each of these puts a button on a Command Bridge hub — that button is the member/staff
        // entry point, so without a configured Command Bridge the feature has nowhere to be reached.
        GuildFeature.RaidAlerts => [new(GuildFeature.CommandBridge, "Members report raids via the Command Bridge button.")],
        GuildFeature.ShieldReminders => [new(GuildFeature.CommandBridge, "Setup + staff shield actions live on the Command Bridge.")],
        GuildFeature.Absences => [new(GuildFeature.CommandBridge, "Members manage absences via the Command Bridge button.")],
        GuildFeature.Announcements => [new(GuildFeature.CommandBridge, "The 'unread announcements' button lives on the Command Bridge.")],
        GuildFeature.AlertsOptIn => [new(GuildFeature.CommandBridge, "Members manage alerts via the Command Bridge button.")],
        GuildFeature.RoeViolationReports => [new(GuildFeature.CommandBridge, "Members report RoE violations via the Command Bridge button.")],
        GuildFeature.Tickets => [new(GuildFeature.CommandBridge, "The 'contact staff' button lives on the Command Bridge.")],
        GuildFeature.AnonymousMessaging => [new(GuildFeature.CommandBridge, "The 'contact staff' button lives on the Command Bridge.")],

        // Assigns the Territory Capture Services role to the alliance's Admiral/Commodore members —
        // it needs TC to own the Services role and Rank Roles to maintain the source rank roles.
        GuildFeature.ServicesRoleSync => [new(GuildFeature.TerritoryCapture, "Provides the Services role to assign."), new(GuildFeature.RankRoles, "Mirrors the Admiral/Commodore rank roles.")],

        _ => [],
    };
}
