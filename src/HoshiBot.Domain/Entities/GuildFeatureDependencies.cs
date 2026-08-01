namespace HoshiBot.Domain.Entities;

// A declared dependency of one feature on another: the required Feature plus whether it carries
// an optional Note explaining any nuance (e.g. a "soft" dependency that other paths can also
// satisfy). The Note's actual text is catalog-driven (Msg.WebGuild.DependencyNote, keyed by the
// (declaring feature, required feature) pair) rather than stored here, so it's authored in both
// languages like every other Web-facing string; HasNote just tells the Web layer whether to look
// one up. There is deliberately no notion of depending on a *subset* of the required feature —
// every current relationship is whole-feature, so the free-text Note carries any subtlety instead.
public readonly record struct FeatureDependency(GuildFeature Feature, bool HasNote = false);

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
        // AI Chat injects it into answers (note: "The collected lore is used to ground AI Chat's
        // answers.").
        GuildFeature.MemberLore => [new(GuildFeature.AiBackend), new(GuildFeature.AiChat, HasNote: true)],

        // The forwarder's translation calls reuse the guild-wide AI backend model/API key
        // (AiChatModelResolver) — without it configured, it has no model to translate with (note:
        // "Uses the AI backend's configured model to translate.").
        GuildFeature.AnnouncementForwarder => [new(GuildFeature.AiBackend, HasNote: true)],

        // /hoshi-say composes its message with the guild-wide AI backend's model — without it
        // configured, there's nothing to compose the text with (note: "Hoshi composes the message
        // with the AI backend's configured model.").
        GuildFeature.HoshiSay => [new(GuildFeature.AiBackend, HasNote: true)],

        // Member Onboarding builds directly on Player Assignment's matcher (it DMs the members
        // Player Assignment couldn't place automatically).
        GuildFeature.MemberOnboarding => [new(GuildFeature.PlayerLink)],

        // Rank/Ops roles and nickname sync all run off the member↔player links Player Assignment
        // creates. Soft: the links can also be made by hand, so this is a strong hint rather than a
        // hard requirement (note: "Player links can also be created by hand.").
        GuildFeature.RankRoles => [new(GuildFeature.PlayerLink, HasNote: true)],
        GuildFeature.OpsLevelRoles => [new(GuildFeature.PlayerLink, HasNote: true)],
        GuildFeature.NicknameSync => [new(GuildFeature.PlayerLink, HasNote: true)],

        // Each of these puts a button on a Command Bridge hub — that button is the member/staff
        // entry point, so without a configured Command Bridge the feature has nowhere to be reached
        // (notes describe which hub button, e.g. "Members report raids via the Command Bridge
        // button.").
        GuildFeature.RaidAlerts => [new(GuildFeature.CommandBridge, HasNote: true)],
        GuildFeature.ShieldReminders => [new(GuildFeature.CommandBridge, HasNote: true)],
        GuildFeature.Absences => [new(GuildFeature.CommandBridge, HasNote: true)],
        GuildFeature.Announcements => [new(GuildFeature.CommandBridge, HasNote: true)],
        GuildFeature.AlertsOptIn => [new(GuildFeature.CommandBridge, HasNote: true)],
        GuildFeature.RoeViolationReports => [new(GuildFeature.CommandBridge, HasNote: true)],
        GuildFeature.Tickets => [new(GuildFeature.CommandBridge, HasNote: true)],
        GuildFeature.AnonymousMessaging => [new(GuildFeature.CommandBridge, HasNote: true)],

        // Assigns the Territory Capture Services role to the alliance's Admiral/Commodore members —
        // it needs TC to own the Services role ("Provides the Services role to assign.") and Rank
        // Roles to maintain the source rank roles ("Mirrors the Admiral/Commodore rank roles.").
        GuildFeature.ServicesRoleSync => [new(GuildFeature.TerritoryCapture, HasNote: true), new(GuildFeature.RankRoles, HasNote: true)],

        // Capture sign-off owns nothing of its own: TC's digests/reminders are what carry the
        // buttons ("The sign-off buttons ride on the capture digests and reminders.") and Absences
        // owns the rows a click writes ("Each sign-off is recorded as an absence."). Both are hard
        // requirements — with either off the feature has nothing to attach to, or writes rows
        // nobody can manage.
        GuildFeature.TerritoryCaptureSignOff => [new(GuildFeature.TerritoryCapture, HasNote: true), new(GuildFeature.Absences, HasNote: true)],

        _ => [],
    };
}
