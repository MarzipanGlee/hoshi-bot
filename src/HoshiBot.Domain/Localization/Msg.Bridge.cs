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

        // The staff bridge's roster-gap list: who is in the alliance in-game but not in this Discord.
        public static string MissingPlayersTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.MissingPlayersTitle");

        public static string MissingPlayersIntro(Language lang, string tag, int missing, int total) =>
            MessageCatalog.Format(lang, "Bridge.MissingPlayersIntro", ("tag", tag), ("missing", missing), ("total", total));

        public static string MissingPlayersNone(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Bridge.MissingPlayersNone", ("tag", tag));

        public static string MissingPlayersNoAlliance(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.MissingPlayersNoAlliance");

        public static string MissingPlayersMore(Language lang, int count) =>
            MessageCatalog.Format(lang, "Bridge.MissingPlayersMore", ("count", count));

        // The two help buttons. The channel guide's body is admin-authored (a setting, not a
        // catalog entry) — only its title and the not-yet-written placeholder live here.
        public static string ChannelGuideTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ChannelGuideTitle");

        public static string ChannelGuideNotConfigured(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.ChannelGuideNotConfigured");

        public static string BotSupportTitle(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.BotSupportTitle");

        public static string BotSupportBody(Language lang, string commander, string channel) =>
            MessageCatalog.Format(lang, "Bridge.BotSupportBody", ("commander", commander), ("channel", channel));

        public static string BotSupportNoChannel(Language lang, string commander) =>
            MessageCatalog.Format(lang, "Bridge.BotSupportNoChannel", ("commander", commander));

        public static string StaffMuteDisableButton(Language lang) =>
            MessageCatalog.Format(lang, "Bridge.StaffMuteDisableButton");
    }
}
