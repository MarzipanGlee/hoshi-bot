namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // The Permission Check page and PermissionAuditService ("Web.Audit.*") — the page's
    // banner, the audit engines' finding/fix-outcome strings and the per-permission
    // display labels. The service takes the Language as an explicit parameter from the
    // calling component; nothing localized is ever cached.
    public static class WebAudit
    {
        public static string Heading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.Heading");

        public static string Lead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.Lead");

        public static string BotTopRoleLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.BotTopRoleLabel");

        public static string HighestRoleInServer(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.HighestRoleInServer");

        public static string RolesAbove(Language lang, int count) =>
            MessageCatalog.FormatCount(lang, "Web.Audit.RolesAbove", count);

        public static string ManageRolesLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.ManageRolesLabel");

        public static string ManageRolesYes(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.ManageRolesYes");

        public static string ManageRolesNo(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.ManageRolesNo");

        public static string RoleTip(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.RoleTip");

        // Per-permission display label ("Web.Audit.Perm.<EnumName>"). Enum-driven keys
        // have no compile check — an unmapped permission falls back to its enum name
        // rather than leaking a raw catalog key into the UI.
        public static string Perm(Language lang, string name)
        {
            var key = $"Web.Audit.Perm.{name}";
            var label = MessageCatalog.Format(lang, key);
            return label == key ? name : label;
        }

        public static string BotLacksManageRoles(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.BotLacksManageRoles");

        public static string BotRoleOutranked(Language lang, string? botRole, string role) =>
            MessageCatalog.Format(lang, "Web.Audit.BotRoleOutranked", ("botRole", botRole), ("role", role));

        public static string BotCannotGrant(Language lang, string permissions) =>
            MessageCatalog.Format(lang, "Web.Audit.BotCannotGrant", ("permissions", permissions));

        public static string BotNoRole(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.BotNoRole");

        public static string ChannelNotFound(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.ChannelNotFound");

        public static string SourceFeature(Language lang, object feature) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceFeature", ("feature", feature));

        public static string SourceFeatureSetting(Language lang, object feature, string key) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceFeatureSetting", ("feature", feature), ("key", key));

        public static string SourceCategoryChild(Language lang, string label, string name) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceCategoryChild", ("label", label), ("name", name));

        public static string SourceAlert(Language lang, object kind) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceAlert", ("kind", kind));

        public static string SourceAllianceBoarding(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceAllianceBoarding", ("tag", tag));

        public static string SourceCommandBridge(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceCommandBridge", ("tag", tag));

        public static string SourceStaffCommandBridge(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceStaffCommandBridge", ("tag", tag));

        public static string SourceFriendsCommandBridge(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceFriendsCommandBridge", ("tag", tag));

        public static string SourceRemindersAllies(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceRemindersAllies", ("tag", tag));

        public static string SourceRulesDe(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceRulesDe", ("tag", tag));

        public static string SourceRulesEn(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceRulesEn", ("tag", tag));

        public static string SourceUserNotifications(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceUserNotifications", ("tag", tag));

        public static string SourceBotSupport(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceBotSupport", ("tag", tag));

        public static string SourceCommandStaffJobs(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceCommandStaffJobs", ("tag", tag));

        public static string FixedViaCategory(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.FixedViaCategory");

        public static string FixedOnChannel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.FixedOnChannel");

        public static string CategoryUpdatedChannelMissing(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.CategoryUpdatedChannelMissing");

        public static string CategoryUpdatedNotSynced(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.CategoryUpdatedNotSynced");

        public static string ChannelFixStillMissing(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.ChannelFixStillMissing");

        public static string FixRefusedVisible(Language lang, string level) =>
            MessageCatalog.Format(lang, "Web.Audit.FixRefusedVisible", ("level", level));

        public static string FixRefusedInvisible(Language lang, string level) =>
            MessageCatalog.Format(lang, "Web.Audit.FixRefusedInvisible", ("level", level));

        public static string FixRejected(Language lang, object status) =>
            MessageCatalog.Format(lang, "Web.Audit.FixRejected", ("status", status));

        public static string LevelCategory(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.LevelCategory");

        public static string LevelChannel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.LevelChannel");

        public static string FixExpectationError(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.FixExpectationError");
    }
}
