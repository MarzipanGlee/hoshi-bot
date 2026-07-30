namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Shared Web admin UI chrome ("Web.Common.*") — the strings owned by the reusable
    // components under Components/Shared (pickers, list editors, cards, confirm/loading
    // shells) plus the Error/NotFound pages. Components consume the layout-cascaded
    // Language ([CascadingParameter] Language Lang); text a CALLER passes in as a
    // parameter is localized at the call site, not here.
    public static class WebCommon
    {
        public static string Loading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.Loading");

        public static string CheckingAccess(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.CheckingAccess");

        public static string Enabled(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.Enabled");

        public static string Add(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.Add");

        public static string Delete(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.Delete");

        public static string DeleteTitle(Language lang, string entity) =>
            MessageCatalog.Format(lang, "Web.Common.DeleteTitle", ("entity", entity));

        public static string DeleteConfirmPrompt(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.DeleteConfirmPrompt");

        public static string BackToList(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.BackToList");

        public static string DiscordUserId(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.DiscordUserId");

        public static string DiscordUserIdPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.DiscordUserIdPlaceholder");

        public static string ChooseFile(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.ChooseFile");

        public static string ChooseFiles(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.ChooseFiles");

        public static string Importing(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.Importing");

        public static string ImportFailed(Language lang, string message) =>
            MessageCatalog.Format(lang, "Web.Common.ImportFailed", ("message", message));

        public static string Channel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.Channel");

        public static string Channels(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.Channels");

        public static string Role(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.Role");

        public static string Roles(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.Roles");

        public static string Regional(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.Regional");

        public static string Region(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.Region");

        public static string Server(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.Server");

        public static string SelectRegionPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.SelectRegionPlaceholder");

        public static string SelectServerPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.SelectServerPlaceholder");

        public static string SelectChannelOption(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.SelectChannelOption");

        public static string SelectRoleOption(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.SelectRoleOption");

        public static string NoneOption(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.NoneOption");

        public static string ChooseRoleOption(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.ChooseRoleOption");

        public static string CreateChannelOption(Language lang, string name) =>
            MessageCatalog.Format(lang, "Web.Common.CreateChannelOption", ("name", name));

        public static string CreateRoleOption(Language lang, string name) =>
            MessageCatalog.Format(lang, "Web.Common.CreateRoleOption", ("name", name));

        // id is a snowflake rendered verbatim — string or ulong depending on the caller.
        public static string Unknown(Language lang, object? id) =>
            MessageCatalog.Format(lang, "Web.Common.Unknown", ("id", id));

        public static string WholeCategory(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.WholeCategory");

        public static string WholeCategoryOption(Language lang, string name) =>
            MessageCatalog.Format(lang, "Web.Common.WholeCategoryOption", ("name", name));

        public static string AlertChannels(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.AlertChannels");

        public static string AlertChannelsUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.AlertChannelsUsage");

        public static string LanguageDefault(Language lang, string inherited) =>
            MessageCatalog.Format(lang, "Web.Common.LanguageDefault", ("inherited", inherited));

        public static string PoweredBy(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.PoweredBy");

        public static string ListAnd(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.ListAnd");

        public static string NeedsSetup(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.NeedsSetup");

        public static string InstallBot(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.InstallBot");

        public static string FeaturesEnabledCount(Language lang, int enabled, int total) =>
            MessageCatalog.Format(lang, "Web.Common.FeaturesEnabledCount", ("enabled", enabled), ("total", total));

        public static string NoAllianceLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.NoAllianceLead");

        public static string NoAllianceLinkText(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.NoAllianceLinkText");

        public static string NoAllianceTail(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.NoAllianceTail");

        public static string CommandStaffRole(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.CommandStaffRole");

        public static string CommandStaffRoleUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.CommandStaffRoleUsage");

        public static string LinkedAlliances(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.LinkedAlliances");

        public static string LinkedServers(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.LinkedServers");

        public static string LinkedVeilGroups(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.LinkedVeilGroups");

        public static string Default(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.Default");

        public static string DefaultColorTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.DefaultColorTitle");

        public static string AllowMention(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.AllowMention");

        public static string AllianceEmblemAlt(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.AllianceEmblemAlt");

        public static string AllianceEmblemTitle(Language lang, int? emblem) =>
            MessageCatalog.Format(lang, "Web.Common.AllianceEmblemTitle", ("emblem", emblem));

        public static string AiHealthDegraded(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.AiHealthDegraded");

        public static string AiHealthEmbedFailing(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.AiHealthEmbedFailing");

        public static string AiHealthChatFailing(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.AiHealthChatFailing");

        public static string AiHealthNever(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.AiHealthNever");

        public static string AiHealthLine(Language lang, string what, string lastError, string detail, string lastSuccess) =>
            MessageCatalog.Format(lang, "Web.Common.AiHealthLine",
                ("what", what), ("lastError", lastError), ("detail", detail), ("lastSuccess", lastSuccess));

        public static string AiHealthViewLink(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.AiHealthViewLink");

        public static string ErrorPageTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.ErrorPageTitle");

        public static string ErrorHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.ErrorHeading");

        public static string ErrorMessage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.ErrorMessage");

        public static string ErrorRequestId(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.ErrorRequestId");

        public static string NotFoundTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.NotFoundTitle");

        public static string NotFoundMessage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.NotFoundMessage");

        public static string NotFoundGoHome(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.NotFoundGoHome");

        public static string DiscordLoadError(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.DiscordLoadError");

        public static string CreateRoleError(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.CreateRoleError");

        public static string CreateChannelError(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.CreateChannelError");

        public static string CreateCategoryError(Language lang) =>
            MessageCatalog.Format(lang, "Web.Common.CreateCategoryError");
    }
}
