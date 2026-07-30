using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // The public landing page's own marketing copy ("Web.Landing.*") — Pages/Index.razor.
    // Nav-chrome bits it also renders (Manage/My Area/log-in-or-out, the audience labels
    // themselves) reuse Msg.WebNav/Msg.WebAudience instead of duplicating those strings here.
    // Some of the "extra" feature-pitch cards below reuse a real feature's Msg.WebFeature.Title
    // where the wording matches exactly; their description is landing-specific pitch copy, kept
    // separate from the admin-facing Msg.WebFeature.Description for the same feature.
    public static class WebLanding
    {
        public static string HeroTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.Hero.Title");

        public static string HeroSubtitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.Hero.Subtitle");

        // "Add to Discord" — shared by the hero and the closing CTA section.
        public static string AddToDiscord(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.AddToDiscord");

        // The longer invite-CTA phrasing of "log in", distinct from Msg.WebNav.LogIn's bare
        // "Log in" used in the site header — shared by the hero and the members section.
        public static string LogInWithDiscord(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.LogInWithDiscord");

        public static string FeaturesTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.Features.Title");

        public static string FeaturesIntro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.Features.Intro");

        // Per-audience section heading, e.g. "For Alliance Discords" — audienceLabel is the
        // already-localized noun from AudienceDisplay/Msg.WebAudience.
        public static string AudienceSectionHeading(Language lang, string audienceLabel) =>
            MessageCatalog.Format(lang, "Web.Landing.Features.ForAudience", ("audience", audienceLabel));

        // Enum-driven keys (no compile check) — an unmapped audience falls back to "" like the
        // sibling Msg.WebAudience.Description, since GuildAudience.Guild/None never render here
        // (the landing page's AudienceSections list never includes them).
        public static string AudienceTagline(Language lang, GuildAudience audience)
        {
            var key = $"Web.Landing.Audience.{audience}.Tagline";
            var text = MessageCatalog.Format(lang, key);
            return text == key ? "" : text;
        }

        public static string AudienceIntro(Language lang, GuildAudience audience)
        {
            var key = $"Web.Landing.Audience.{audience}.Intro";
            var text = MessageCatalog.Format(lang, key);
            return text == key ? "" : text;
        }

        public static string MembersTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.Members.Title");

        public static string MembersIntro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.Members.Intro");

        public static string OpenMyArea(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.Members.OpenMyArea");

        public static string CtaTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.Cta.Title");

        public static string CtaSubtitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.Cta.Subtitle");

        // "Extra" feature-pitch cards: real bot capabilities the landing page advertises that
        // aren't part of the audience-filtered FeatureCatalog loop (see Index.razor's own
        // comment for why each one isn't just picked up automatically).
        public static string SetupWizardDescription(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.ExtraCard.SetupWizard.Description");

        public static string AnnouncementForwarderPitch(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.ExtraCard.AnnouncementForwarder.Description");

        public static string MemberSyncTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.ExtraCard.MemberSync.Title");

        public static string MemberSyncDescription(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.ExtraCard.MemberSync.Description");

        public static string MemberOnboardingPitch(Language lang) =>
            MessageCatalog.Format(lang, "Web.Landing.ExtraCard.MemberOnboarding.Description");
    }
}
