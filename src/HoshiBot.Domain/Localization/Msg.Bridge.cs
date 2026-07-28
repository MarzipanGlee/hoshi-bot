namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Command Bridge hub flows (CommandBridgeButtonModule): raid/shield prompts, the
    // contact-command-staff step, unread announcements, RoE entry buttons, alert opt-ins.
    public static class Bridge
    {
        public static string RaidTargetPrompt(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.RaidTargetPrompt");

        public static string RaidModalTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.RaidModalTitle");

        public static string RaidServerPrompt(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.RaidServerPrompt");

        public static string HomeServerButton(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.HomeServerButton");

        public static string EnemyServerButton(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.EnemyServerButton");

        public static string LocationLabel(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.LocationLabel");

        public static string SystemPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.SystemPlaceholder");

        public static string AttackerLabel(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AttackerLabel");

        public static string AttackerPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AttackerPlaceholder");

        public static string ShieldModalTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ShieldModalTitle");

        public static string ShieldDurationLabel(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ShieldDurationLabel");

        public static string ShieldDurationPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ShieldDurationPlaceholder");

        public static string DraftNotFound(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.DraftNotFound");

        public static string DraftUnknownKind(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.DraftUnknownKind");

        public static string Cancelled(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.Cancelled");

        public static string ContactTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ContactTitle");

        // options: the pre-joined ContactTicketOption/ContactAnonymousOption lines.
        public static string ContactIntro(Language lang, string name, string options) =>
            MessageCatalog.Format(lang, "Bridge.ContactIntro", ("name", name), ("options", options));

        public static string ContactTicketOption(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ContactTicketOption");

        public static string ContactAnonymousOption(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ContactAnonymousOption");

        // Doubles as the contact-step button label and the modal title.
        public static string TicketOpen(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.TicketOpen");

        public static string AnonymousMessage(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AnonymousMessage");

        public static string FeatureDisabledHere(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.FeatureDisabledHere");

        public static string SubjectLabel(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.SubjectLabel");

        public static string SubjectPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.SubjectPlaceholder");

        public static string MessageLabel(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.MessageLabel");

        public static string MessagePlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.MessagePlaceholder");

        public static string AnnouncementsUnreadTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AnnouncementsUnreadTitle");

        public static string AnnouncementsAllRead(Language lang, string name) =>
            MessageCatalog.Format(lang, "Bridge.AnnouncementsAllRead", ("name", name));

        public static string AnnouncementsUnreadIntro(Language lang, string name, string list) =>
            MessageCatalog.Format(lang, "Bridge.AnnouncementsUnreadIntro", ("name", name), ("list", list));

        public static string RoeToMe(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.RoeToMe");

        public static string RoeFromMe(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.RoeFromMe");

        public static string RoeByOwnPlayer(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.RoeByOwnPlayer");

        public static string RoePromptBody(Language lang, string name) =>
            MessageCatalog.Format(lang, "Bridge.RoePromptBody", ("name", name));

        public static string RoeOtherPrompt(Language lang, string name) =>
            MessageCatalog.Format(lang, "Bridge.RoeOtherPrompt", ("name", name));

        public static string AlertsTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AlertsTitle");

        public static string AlertsNoRoles(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AlertsNoRoles");

        public static string AlertsOn(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AlertsOn");

        public static string AlertsOff(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.AlertsOff");

        public static string AlertsIntro(Language lang, string list) =>
            MessageCatalog.Format(lang, "Bridge.AlertsIntro", ("list", list));

        // /post-command-bridge results (CommandBridgeAdminModule).
        public static string NoAllianceLinked(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.NoAllianceLinked");

        public static string HubUpdated(Language lang, string tag, string kind) =>
            MessageCatalog.Format(lang, "Bridge.HubUpdated", ("tag", tag), ("kind", kind));

        public static string HubPosted(Language lang, string tag, string kind) =>
            MessageCatalog.Format(lang, "Bridge.HubPosted", ("tag", tag), ("kind", kind));

        public static string HubNoChannel(Language lang, string tag, string kind) =>
            MessageCatalog.Format(lang, "Bridge.HubNoChannel", ("tag", tag), ("kind", kind));

        public static string KindUser(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.KindUser");

        public static string KindStaff(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.KindStaff");

        public static string KindFriends(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.KindFriends");

        // The hub message itself (CommandBridgeHubService).
        public static string HubDescription(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.HubDescription");

        public static string HubTitleUser(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.HubTitleUser");

        public static string HubTitleStaff(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.HubTitleStaff");

        public static string HubTitleFriends(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.HubTitleFriends");

        // The multi-audience variant of the contact-staff hub button; the single-audience
        // variant reuses ContactTitle (same wording).
        public static string ContactStaffAudience(Language lang, string audience) =>
            MessageCatalog.Format(lang, "Bridge.ContactStaffAudience", ("audience", audience));

        // The modal-retry step (CommandBridgeModalModule + PendingModalInputService buttons).
        public static string InvalidInputTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.InvalidInputTitle");

        public static string ShieldDurationParseError(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ShieldDurationParseError");

        public static string BackButton(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.BackButton");

        public static string CancelButton(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.CancelButton");

        // Staff bridge: beta-tester self-service toggle (CommandBridgeStaffBetaModule).
        public static string BetaTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.BetaTitle");

        public static string BetaRoleNotConfigured(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.BetaRoleNotConfigured");

        public static string BetaStatus(Language lang, string status) =>
            MessageCatalog.Format(lang, "Bridge.BetaStatus", ("status", status));

        public static string BetaOn(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.BetaOn");

        public static string BetaOff(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.BetaOff");

        public static string BetaEnableButton(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.BetaEnableButton");

        public static string BetaDisableButton(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.BetaDisableButton");

        public static string BetaEnabled(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.BetaEnabled");

        public static string BetaDisabled(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.BetaDisabled");

        public static string BetaToggleFailed(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.BetaToggleFailed");

        public static string BetaActionAdjustRole(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.BetaActionAdjustRole");

        public static string BetaHintManageRoles(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.BetaHintManageRoles");

        // Staff bridge: shield-loss report and mute management
        // (CommandBridgeStaffButtonModule + CommandBridgeStaffMenuModule).
        public static string StaffShieldTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.StaffShieldTitle");

        public static string StaffShieldTargetPrompt(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.StaffShieldTargetPrompt");

        public static string StaffMuteTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.StaffMuteTitle");

        public static string StaffMuteTargetPrompt(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.StaffMuteTargetPrompt");

        public static string StaffMuteStatus(Language lang, string user, string status) =>
            MessageCatalog.Format(lang, "Bridge.StaffMuteStatus", ("user", user), ("status", status));

        public static string StaffMuteStateOn(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.StaffMuteStateOn");

        public static string StaffMuteStateOff(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.StaffMuteStateOff");

        public static string StaffMuteEnableButton(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.StaffMuteEnableButton");

        public static string StaffMuteDisableButton(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.StaffMuteDisableButton");
    }
}
