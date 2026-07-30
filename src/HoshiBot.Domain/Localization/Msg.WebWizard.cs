namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // The guild Setup Wizard ("Web.Wizard.*") — step titles, per-step copy, form labels
    // and the wizard's own error/status lines. The Audience step title reuses
    // Msg.WebNav.Audience and the Command Staff Role label Msg.WebCommon.CommandStaffRole.
    public static class WebWizard
    {
        public static string PageTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.PageTitle");

        public static string StepHeader(Language lang, int step, int total, string title) =>
            MessageCatalog.Format(lang, "Web.Wizard.StepHeader", ("step", step), ("total", total), ("title", title));

        public static string StepWelcome(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.StepWelcome");

        public static string StepScope(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.StepScope");

        public static string StepCoreChannels(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.StepCoreChannels");

        public static string StepAdminAccess(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.StepAdminAccess");

        public static string StepReview(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.StepReview");

        public static string WelcomeLead1(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.WelcomeLead1");

        public static string WelcomeLead2(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.WelcomeLead2");

        public static string AudienceLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.AudienceLead");

        public static string ScopeLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.ScopeLead");

        public static string CoreLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.CoreLead");

        public static string CoreLeadLink(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.CoreLeadLink");

        public static string CoreLeadTail(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.CoreLeadTail");

        public static string CoreLeaveBlank(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.CoreLeaveBlank");

        public static string Creating(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.Creating");

        public static string CategoryLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.CategoryLabel");

        public static string CategoryNoneOption(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.CategoryNoneOption");

        public static string Or(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.Or");

        public static string NewCategoryPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.NewCategoryPlaceholder");

        public static string LogChannelLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.LogChannelLabel");

        public static string AdminChannelLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.AdminChannelLabel");

        public static string MemberRoleLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.MemberRoleLabel");

        public static string CreateAutomaticallyOption(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.CreateAutomaticallyOption");

        public static string AdminAccessLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.AdminAccessLead");

        public static string ReviewAudienceLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.ReviewAudienceLabel");

        public static string NotSet(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.NotSet");

        public static string ReviewLinksLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.ReviewLinksLabel");

        public static string ReviewAdminRolesLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.ReviewAdminRolesLabel");

        public static string ReviewLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.ReviewLead");

        public static string FinishButton(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.FinishButton");

        public static string Back(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.Back");

        public static string Next(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.Next");

        public static string Saving(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.Saving");

        public static string CreateError(Language lang) =>
            MessageCatalog.Format(lang, "Web.Wizard.CreateError");
    }
}
