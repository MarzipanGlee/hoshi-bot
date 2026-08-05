using HoshiBot.Domain.Entities;

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

        // The same enum-keyed pattern for a channel's access profile ("Web.Audit.Profile.<Name>") —
        // what the bot DOES in a channel, which is more use to an admin than a list of bits.
        public static string Profile(Language lang, ChannelAccessProfile profile)
        {
            var key = $"Web.Audit.Profile.{profile}";
            var label = MessageCatalog.Format(lang, key);
            return label == key ? profile.ToString() : label;
        }

        public static string GuildWideGroup(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.GuildWideGroup");

        public static string ServerPermissionsTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.ServerPermissionsTitle");

        public static string ServerPermissionsMissing(Language lang, string permissions) =>
            MessageCatalog.Format(lang, "Web.Audit.ServerPermissionsMissing", ("permissions", permissions));

        public static string ServerPermissionsNeededBy(Language lang, string features) =>
            MessageCatalog.Format(lang, "Web.Audit.ServerPermissionsNeededBy", ("features", features));

        public static string RequiredPermsHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.RequiredPermsHeading");

        public static string RequiredPermsOk(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.RequiredPermsOk");

        public static string RequiredPermsNote(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.RequiredPermsNote");

        public static string ReauthorizeRequiredButton(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.ReauthorizeRequiredButton");

        public static string OptionalPermsHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.OptionalPermsHeading");

        public static string OptionalPermsOk(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.OptionalPermsOk");

        public static string OptionalPermsMissing(Language lang, string permissions) =>
            MessageCatalog.Format(lang, "Web.Audit.OptionalPermsMissing", ("permissions", permissions));

        public static string OptionalPermsNote(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.OptionalPermsNote");

        public static string ReauthorizeOptionalButton(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.ReauthorizeOptionalButton");

        public static string FeatureAccessTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.FeatureAccessTitle");

        public static string FeatureAccessIntro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.FeatureAccessIntro");

        public static string NothingConfiguredStatus(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.NothingConfiguredStatus");

        public static string ChannelGoneStatus(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.ChannelGoneStatus");

        public static string DoesColumn(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.DoesColumn");

        public static string UnauditableNote(Language lang, string features) =>
            MessageCatalog.Format(lang, "Web.Audit.UnauditableNote", ("features", features));

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


        public static string SourceCommandBridge(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceCommandBridge", ("tag", tag));

        public static string SourceStaffCommandBridge(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceStaffCommandBridge", ("tag", tag));

        public static string SourceFriendsCommandBridge(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Audit.SourceFriendsCommandBridge", ("tag", tag));







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

        // ExpectationEditor.razor — the saved-expectations table plus its add/edit form.
        // Channel/Role/Add/Delete/Save/Cancel and the select placeholders reuse Msg.WebCommon;
        // only the pieces specific to this table/form get their own keys here.
        public static string ExpectedPermissionsTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.ExpectedPermissionsTitle");

        public static string AllowColumn(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.AllowColumn");

        public static string DenyColumn(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.DenyColumn");

        public static string AddExpectationTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.AddExpectationTitle");

        public static string EditExpectationTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.EditExpectationTitle");

        public static string PermissionColumn(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.PermissionColumn");

        public static string NeutralColumn(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.NeutralColumn");

        // ExpectationAuditSection.razor — the expectation-vs-live diff table.
        public static string AuditTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.AuditTitle");

        // Shared by both this section's "Run Audit" button and BotAccessSection's "Re-check".
        public static string CheckingEllipsis(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.CheckingEllipsis");

        public static string RunAudit(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.RunAudit");

        public static string StatusColumn(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.StatusColumn");

        public static string DetailsColumn(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.DetailsColumn");

        // Shared by both this section's match/mismatch cell and BotAccessSection's access cell.
        public static string OkStatus(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.OkStatus");

        public static string MismatchStatus(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.MismatchStatus");

        public static string MissingAllowLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.MissingAllowLabel");

        public static string UnexpectedAllowLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.UnexpectedAllowLabel");

        public static string MissingDenyLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.MissingDenyLabel");

        public static string UnexpectedDenyLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.UnexpectedDenyLabel");

        // Shared by both this section's per-row fix button and BotAccessSection's fix buttons.
        public static string FixButton(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.FixButton");

        // BotAccessSection.razor — the configured post-to channels vs. the bot's live access.
        public static string BotPostingAccessTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.BotPostingAccessTitle");

        public static string BotPostingAccessIntro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.BotPostingAccessIntro");

        public static string ReCheck(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.ReCheck");

        public static string NoPostToChannelsConfigured(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.NoPostToChannelsConfigured");

        public static string CantFixCalloutTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.CantFixCalloutTitle");

        // The callout's intro paragraph, four <ol> steps, and closing paragraph — each contains
        // <strong>/<em> markup, so render with @((MarkupString)...); the <p>/<ol>/<li> wrapper
        // tags themselves stay in BotAccessSection.razor.
        public static string CantFixCalloutIntro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.CantFixCalloutIntro");

        public static string CantFixStep1(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.CantFixStep1");

        public static string CantFixStep2(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.CantFixStep2");

        public static string CantFixStep3(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.CantFixStep3");

        public static string CantFixStep4(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.CantFixStep4");

        public static string CantFixCalloutOutro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.CantFixCalloutOutro");

        public static string UsedByColumn(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.UsedByColumn");

        public static string MissingAccessStatus(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.MissingAccessStatus");

        public static string ChannelStatusLine(Language lang, string perms) =>
            MessageCatalog.Format(lang, "Web.Audit.ChannelStatusLine", ("perms", perms));

        public static string CategoryStatusLine(Language lang, string perms) =>
            MessageCatalog.Format(lang, "Web.Audit.CategoryStatusLine", ("perms", perms));

        public static string FixOnCategoryButton(Language lang, string name) =>
            MessageCatalog.Format(lang, "Web.Audit.FixOnCategoryButton", ("name", name));

        public static string FixOnChannelButton(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.FixOnChannelButton");

        public static string CantFixAutomaticallyNote(Language lang) =>
            MessageCatalog.Format(lang, "Web.Audit.CantFixAutomaticallyNote");
    }
}
