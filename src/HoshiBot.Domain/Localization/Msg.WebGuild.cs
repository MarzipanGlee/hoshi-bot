using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Guild admin pages ("Web.Guild.*") — the /manage guilds dashboard, the guild Overview,
    // Audience/AudienceSettings/Settings pages and the content strings of their shared
    // editors (SettingsEditor, ScopeEditor, AudienceEditor). Components consume the
    // layout-cascaded Language ([CascadingParameter] Language Lang).
    public static class WebGuild
    {
        public static string Welcome(Language lang, string? name) =>
            MessageCatalog.Format(lang, "Web.Guild.Welcome", ("name", name));

        public static string NeedsSetup(Language lang, int count) =>
            MessageCatalog.FormatCount(lang, "Web.Guild.NeedsSetup", count);

        public static string YourGuilds(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.YourGuilds");

        public static string NoGuilds(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.NoGuilds");

        public static string ForeignServers(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.ForeignServers");

        public static string SupportBadge(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.SupportBadge");

        public static string ForeignServersLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.ForeignServersLead");

        public static string LogInWithDiscord(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.LogInWithDiscord");

        public static string LogInTail(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.LogInTail");

        public static string Alliances(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.Alliances");

        public static string AlliancesLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AlliancesLead");

        public static string NotConfiguredYet(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.NotConfiguredYet");

        public static string MissingManageRoles(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.MissingManageRoles");

        public static string RolesAboveBot(Language lang, int count) =>
            MessageCatalog.FormatCount(lang, "Web.Guild.RolesAboveBot", count);

        public static string PermissionsOk(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.PermissionsOk");

        public static string DiscordUnreachable(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.DiscordUnreachable");

        public static string EnabledCount(Language lang, int enabled, int total) =>
            MessageCatalog.Format(lang, "Web.Guild.EnabledCount", ("enabled", enabled), ("total", total));

        public static string SettingsSubtitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.SettingsSubtitle");

        public static string SetupComplete(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.SetupComplete");

        public static string NeedsSetupSubtitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.NeedsSetupSubtitle");

        public static string AudienceLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AudienceLead");

        // The Community audience has no linkable scope, so its settings page says so where the
        // other audiences show their linked-things table.
        public static string NothingToLinkCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.NothingToLinkCardTitle");

        public static string NothingToLinkUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.NothingToLinkUsage");

        public static string Configuration(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.Configuration");

        public static string AudienceSettings(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AudienceSettings");

        public static string AudienceSettingsLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AudienceSettingsLead");

        public static string AudienceSettingsTitle(Language lang, string audience) =>
            MessageCatalog.Format(lang, "Web.Guild.AudienceSettingsTitle", ("audience", audience));

        public static string AudienceSettingsHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AudienceSettingsHeading");

        public static string UnknownAudienceLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.UnknownAudienceLead");

        public static string UnknownAudienceTail(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.UnknownAudienceTail");

        public static string AudienceSettingsIntro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AudienceSettingsIntro");

        public static string AudienceSettingsIntroTail(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AudienceSettingsIntroTail");

        public static string LanguageTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.LanguageTitle");

        public static string AudienceLanguageUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AudienceLanguageUsage");

        public static string GuildLanguage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.GuildLanguage");

        public static string GuildLanguageInherited(Language lang, string name) =>
            MessageCatalog.Format(lang, "Web.Guild.GuildLanguageInherited", ("name", name));

        // Features/Index.razor's own PageTitle/<h1>, e.g. "Alliance Features" — audienceLabel is
        // the already-localized noun from AudienceDisplay/Msg.WebAudience (Guild's own label for
        // the bare, audience-less /features route).
        public static string AudienceFeaturesHeading(Language lang, string audienceLabel) =>
            MessageCatalog.Format(lang, "Web.Guild.AudienceFeaturesHeading", ("audience", audienceLabel));

        public static string SettingsLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.SettingsLead");

        public static string LogTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.LogTitle");

        public static string LogUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.LogUsage");

        public static string AdminTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AdminTitle");

        public static string AdminUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AdminUsage");

        public static string UserLogTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.UserLogTitle");

        public static string UserLogUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.UserLogUsage");

        public static string CrewsTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.CrewsTitle");

        public static string BetaTesterTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.BetaTesterTitle");

        public static string HoshiTesterTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.HoshiTesterTitle");

        public static string GuildLanguageUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.GuildLanguageUsage");

        public static string DiscordLanguageInherited(Language lang, string name) =>
            MessageCatalog.Format(lang, "Web.Guild.DiscordLanguageInherited", ("name", name));

        public static string TagHeader(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.TagHeader");

        public static string AllianceNameHeader(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AllianceNameHeader");

        public static string AllianceLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AllianceLabel");

        public static string SelectAlliancePlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.SelectAlliancePlaceholder");

        public static string AllianceLinkHintLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AllianceLinkHintLead");

        public static string AllianceLinkHintTail(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.AllianceLinkHintTail");

        public static string RemoveAllianceConfirm(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.RemoveAllianceConfirm");

        public static string VeilGroupLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.VeilGroupLabel");

        public static string SelectVeilGroupPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.SelectVeilGroupPlaceholder");

        public static string Configure(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.Configure");

        // Features/Index.razor's own chrome (heading built via AudienceFeaturesHeading above).
        public static string HideInactiveFeatures(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.HideInactiveFeatures");

        public static string NoFeaturesAvailable(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.NoFeaturesAvailable");

        // "Requires:" label ahead of a feature card's dependency links — DependencyNotEnabled's
        // "not enabled" badge reuses the FeatureSettings dependency-line wording above.
        public static string Requires(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.Requires");

        // FeatureSettings.razor — the shell around every per-feature editor.
        public static string FeatureSettingsTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.FeatureSettingsTitle");

        public static string UnknownFeature(Language lang, string slug) =>
            MessageCatalog.Format(lang, "Web.Guild.UnknownFeature", ("slug", slug));

        public static string Redirecting(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.Redirecting");

        public static string RequiresOtherFeatures(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.RequiresOtherFeatures");

        public static string NeedsOtherFeatures(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.NeedsOtherFeatures");

        public static string DependencyEnabled(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.DependencyEnabled");

        public static string DependencyNotConfigured(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.DependencyNotConfigured");

        public static string DependencyNotEnabled(Language lang) =>
            MessageCatalog.Format(lang, "Web.Guild.DependencyNotEnabled");

        // The optional nuance GuildFeatureDependencies attaches to one (owner, dependency) pair
        // (e.g. "Player links can also be created by hand.") — enum-driven with no compile check,
        // so an unmapped pair falls back to "" (FeatureSettings.razor only renders it when non-empty)
        // rather than leaking a raw catalog key. feature is the one declaring the dependency,
        // dependency the required feature it's shown next to.
        public static string DependencyNote(Language lang, GuildFeature feature, GuildFeature dependency)
        {
            var key = $"Web.Guild.DependencyNote.{feature}.{dependency}";
            var note = MessageCatalog.Format(lang, key);
            return note == key ? "" : note;
        }
    }
}
