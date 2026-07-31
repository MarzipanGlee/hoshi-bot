using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Free-prose editor text ("Web.Editor.<Feature>.<Name>") for the per-feature Web
    // editors — intros, list headings, option labels, statuses, buttons: everything not
    // tied to one stored setting key. Card strings that DO belong to a stored setting go
    // through the dynamic Msg.WebEditor.Label/CardTitle/Usage helper instead (keyed by
    // the *SettingKeys constants). One nested class per feature that needs any.

    public static class WebAiBackend
    {
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.Intro");

        public static string ProviderOptionGemini(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.ProviderOptionGemini");

        public static string ProviderOptionOllama(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.ProviderOptionOllama");

        public static string KeySaved(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.KeySaved");

        public static string KeySavedPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.KeySavedPlaceholder");

        public static string EnterKeyPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.EnterKeyPlaceholder");

        public static string ModelLabelOllama(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.ModelLabelOllama");

        public static string ModelLabelGemini(Language lang, string model) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.ModelLabelGemini", ("model", model));

        public static string ServerDefaultModelPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.ServerDefaultModelPlaceholder");

        public static string EmbeddingOptionOllama(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.EmbeddingOptionOllama");

        public static string EmbeddingOptionGemini1(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.EmbeddingOptionGemini1");

        public static string EmbeddingOptionGemini2(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.EmbeddingOptionGemini2");

        public static string ServerDefaultPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.ServerDefaultPlaceholder");

        // Contains inline <code> markup — render via MarkupString (catalog-authored, no user input).
        public static string GateUsageOllama(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.GateUsageOllama");

        public static string DefaultModelPlaceholder(Language lang, string model) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.DefaultModelPlaceholder", ("model", model));

        // Contains inline <code> markup — render via MarkupString (model is a code constant).
        public static string GateUsageGemini(Language lang, string model) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.GateUsageGemini", ("model", model));

        // Contains inline <code> markup — render via MarkupString (model is a code constant).
        public static string RouterTip(Language lang, string model) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.RouterTip", ("model", model));

        public static string RouterOffOllamaPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.RouterOffOllamaPlaceholder");

        public static string RouterOffGeminiPlaceholder(Language lang, string model) =>
            MessageCatalog.Format(lang, "Web.Editor.AiBackend.RouterOffGeminiPlaceholder", ("model", model));
    }

    public static class WebAiChat
    {
        // Intro paragraphs contain inline <strong>/<em> markup — render via MarkupString.
        public static string Intro1(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.Intro1");

        public static string Intro2(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.Intro2");

        public static string HealthLink(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.HealthLink");

        public static string OpenMemoriesLink(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.OpenMemoriesLink");

        public static string ListenTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.ListenTitle");

        public static string ListenUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.ListenUsage");

        public static string KnowledgePreferredTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.KnowledgePreferredTitle");

        public static string KnowledgePreferredUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.KnowledgePreferredUsage");

        public static string KnowledgeNormalTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.KnowledgeNormalTitle");

        public static string KnowledgeNormalUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.KnowledgeNormalUsage");

        public static string KnowledgeLastResortTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.KnowledgeLastResortTitle");

        public static string KnowledgeLastResortUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChat.KnowledgeLastResortUsage");
    }

    public static class WebAllianceTournament
    {
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTournament.Intro");
    }

    public static class WebInfiniteIncursions
    {
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.InfiniteIncursions.Intro");
    }

    public static class WebAnnouncementForwarder
    {
        public static string SourcesTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AnnouncementForwarder.SourcesTitle");

        public static string SourcesUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AnnouncementForwarder.SourcesUsage");

        public static string ServerLanguageOption(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AnnouncementForwarder.ServerLanguageOption");
    }

    public static class WebClientRelease
    {
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ClientRelease.Intro");

        public static string ChannelsTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ClientRelease.ChannelsTitle");

        public static string ChannelsUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ClientRelease.ChannelsUsage");

        public static string PlatformRolesTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ClientRelease.PlatformRolesTitle");

        public static string PlatformRolesUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ClientRelease.PlatformRolesUsage");
    }

    public static class WebCommandBridge
    {
        // Contains inline <strong> markup — render via MarkupString.
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.Intro");

        public static string UserBridgeHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.UserBridgeHeading");

        public static string UserBridgeSubtitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.UserBridgeSubtitle");

        public static string StaffBridgeHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.StaffBridgeHeading");

        public static string StaffBridgeSubtitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.StaffBridgeSubtitle");

        public static string FriendsBridgeHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.FriendsBridgeHeading");

        public static string FriendsBridgeSubtitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.FriendsBridgeSubtitle");

        public static string ChannelUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.ChannelUsage");

        public static string Publish(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.Publish");

        public static string StatusQueued(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.StatusQueued");

        public static string StatusPosted(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.StatusPosted");

        public static string StatusStillQueued(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.StatusStillQueued");

        public static string ButtonsHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.ButtonsHeading");

        public static string NoButtons(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.NoButtons");

        public static string Hidden(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.CommandBridge.Hidden");
    }

    public static class WebMemberLore
    {
        public static string CampaignLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLore.CampaignLabel");

        public static string CampaignUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLore.CampaignUsage");

        public static string ProgressLine(Language lang, int invited, int inProgress, int completed, int declined, int undeliverable) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLore.ProgressLine",
                ("invited", invited), ("inProgress", inProgress), ("completed", completed),
                ("declined", declined), ("undeliverable", undeliverable));

        public static string ViewInterviewsLink(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLore.ViewInterviewsLink");

        public static string NotesCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLore.NotesCardTitle");

        public static string NotesUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLore.NotesUsage");

        public static string OpenNotesLink(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLore.OpenNotesLink");

        // Shared by the member-notes review UI (MemberNotesAdmin's per-field labels and its
        // suggestion badges) and, going forward, the member's own /me lore page — one canonical
        // label per MemberNoteField instead of the two slightly different wordings the admin page
        // used to hardcode in German. Enum-driven with no compile check, so an unmapped value falls
        // back to its raw name rather than leaking a catalog key.
        public static string NoteFieldLabel(Language lang, MemberNoteField field)
        {
            var key = $"Web.Editor.MemberLore.NoteField.{field}";
            var label = MessageCatalog.Format(lang, key);
            return label == key ? field.ToString() : label;
        }
    }

    // MemberLore's "Interviews" extra admin page (MemberInterviewsAdmin.razor).
    public static class WebMemberLoreInterviewsAdmin
    {
        public static string Heading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLoreInterviewsAdmin.Heading");

        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLoreInterviewsAdmin.Intro");

        public static string Empty(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLoreInterviewsAdmin.Empty");

        public static string ColMember(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLoreInterviewsAdmin.ColMember");

        public static string ColStatus(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLoreInterviewsAdmin.ColStatus");

        public static string ColInvited(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLoreInterviewsAdmin.ColInvited");

        public static string ColLastActivity(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLoreInterviewsAdmin.ColLastActivity");

        public static string ColCompleted(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberLoreInterviewsAdmin.ColCompleted");

        public static string Status(Language lang, MemberInterviewStatus status)
        {
            var key = $"Web.Editor.MemberLoreInterviewsAdmin.Status.{status}";
            var label = MessageCatalog.Format(lang, key);
            return label == key ? status.ToString() : label;
        }
    }

    // MemberLore's "Notes & Review" extra admin page (MemberNotesAdmin.razor) — previously
    // hardcoded entirely in German even though every other extra page is English-authored; en is
    // therefore an authored translation rather than a byte-identical extraction (see the batch-4
    // localization notes).
    public static class WebMemberNotesAdmin
    {
        public static string Heading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.Heading");

        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.Intro");

        public static string ToReviewHeading(Language lang, int count) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.ToReviewHeading", ("count", count));

        public static string NoSuggestions(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.NoSuggestions");

        // Kept as two bare connecting words (not one "from {source} about {target}" template) so the
        // razor can wrap the actually-untrusted SourceName/TargetNameRaw values in normal Razor
        // expressions — auto-HTML-encoded — rather than splicing member-supplied text into a
        // MarkupString, which would be an XSS hole.
        public static string FromWord(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.FromWord");

        public static string AboutWord(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.AboutWord");

        public static string ForMemberLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.ForMemberLabel");

        public static string ChooseOption(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.ChooseOption");

        public static string TextLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.TextLabel");

        public static string Approve(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.Approve");

        public static string Reject(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.Reject");

        public static string MembersHeading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.MembersHeading");

        public static string CreateNoteLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.CreateNoteLabel");

        public static string CreateButton(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.CreateButton");

        public static string NoNotes(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.NoNotes");

        public static string PeerLoreHiddenBadge(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.PeerLoreHiddenBadge");

        public static string UnhideButton(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberNotesAdmin.UnhideButton");
    }

    public static class WebMemberOnboarding
    {
        public static string HowItWorksUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberOnboarding.HowItWorksUsage");

        public static string CampaignLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberOnboarding.CampaignLabel");

        public static string CampaignUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberOnboarding.CampaignUsage");

        // Fixes a German-in-English-UI bug: this line ("Offen X · DM gesendet Y · …") was hardcoded
        // in German while the rest of the editor is English.
        public static string ProgressLine(Language lang, int unresolved, int dmSent, int resolved, int declined, int undeliverable) =>
            MessageCatalog.Format(lang, "Web.Editor.MemberOnboarding.ProgressLine",
                ("unresolved", unresolved), ("dmSent", dmSent), ("resolved", resolved),
                ("declined", declined), ("undeliverable", undeliverable));
    }

    public static class WebNicknameSync
    {
        public static string HowItWorksUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.NicknameSync.HowItWorksUsage");

        public static string AddRoleLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.NicknameSync.AddRoleLabel");

        public static string AddRolePlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.NicknameSync.AddRolePlaceholder");

        public static string ModeNever(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.NicknameSync.ModeNever");

        public static string ModeForeignOnly(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.NicknameSync.ModeForeignOnly");

        public static string ModeAlways(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.NicknameSync.ModeAlways");
    }

    public static class WebPlayerLink
    {
        public static string HowItWorksUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerLink.HowItWorksUsage");

        public static string OpenAssignmentsLink(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerLink.OpenAssignmentsLink");

        public static string StatusCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerLink.StatusCardTitle");

        // Contains inline <em> markup — render via MarkupString.
        public static string StatusUsage(Language lang, int unresolvedCount) =>
            MessageCatalog.FormatCount(lang, "Web.Editor.PlayerLink.StatusUsage", unresolvedCount);
    }

    // PlayerLink's "Player Assignments" extra admin page (PlayerAssignmentsAdmin.razor).
    public static class WebPlayerAssignmentsAdmin
    {
        public static string Loading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerAssignmentsAdmin.Loading");

        public static string Heading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerAssignmentsAdmin.Heading");

        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerAssignmentsAdmin.Intro");

        public static string SearchMemberLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerAssignmentsAdmin.SearchMemberLabel");

        public static string SearchPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerAssignmentsAdmin.SearchPlaceholder");

        public static string OnlyUnassigned(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerAssignmentsAdmin.OnlyUnassigned");

        public static string MembersCount(Language lang, int filtered, int total) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerAssignmentsAdmin.MembersCount", ("filtered", filtered), ("total", total));

        public static string ColMember(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerAssignmentsAdmin.ColMember");

        public static string ColAssignedPlayers(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerAssignmentsAdmin.ColAssignedPlayers");

        public static string RepresentsTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerAssignmentsAdmin.RepresentsTitle");

        public static string UseHereTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerAssignmentsAdmin.UseHereTitle");

        public static string SearchPlayerPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerAssignmentsAdmin.SearchPlayerPlaceholder");

        public static string AddPlayerButton(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.PlayerAssignmentsAdmin.AddPlayerButton");
    }

    public static class WebServerStatus
    {
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServerStatus.Intro");
    }

    public static class WebServicesRoleSync
    {
        // Contains inline <strong> markup — render via MarkupString.
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServicesRoleSync.Intro");
    }

    public static class WebStfcNews
    {
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.StfcNews.Intro");
    }

    public static class WebTerritoryCapture
    {
        public static string ServiceSelectionLink(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.TerritoryCapture.ServiceSelectionLink");

        // Contains inline <strong>/<a> markup (the alliance timezone + Alliance Settings link) —
        // render via MarkupString. href is built by the caller from the current route.
        public static string DigestScheduleUsage(Language lang, string timezone, string href) =>
            MessageCatalog.Format(lang, "Web.Editor.TerritoryCapture.DigestScheduleUsage", ("timezone", timezone), ("href", href));

        public static string WeeklyDigestTimeLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.TerritoryCapture.WeeklyDigestTimeLabel");

        public static string DailyDigestTimeLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.TerritoryCapture.DailyDigestTimeLabel");

        public static string AbsenceSignOffLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.TerritoryCapture.AbsenceSignOffLabel");

        // Contains inline <strong> markup (the required Absences feature) — render via MarkupString.
        public static string AbsenceSignOffUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.TerritoryCapture.AbsenceSignOffUsage");
    }

    // TerritoryCapture's "Service Selection" extra admin page (ServiceSelectionAdmin.razor).
    public static class WebServiceSelectionAdmin
    {
        // Contains inline <strong> markup — render via MarkupString. Fixes a German-in-English-UI
        // bug: "Dienste aktivieren"/"obligatorische Dienste"/"optionale Dienste, auf Anfrage" were
        // hardcoded German fragments inside this otherwise-English page.
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.Intro");

        public static string AllianceCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.AllianceCardTitle");

        public static string ChooseAlliancePlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.ChooseAlliancePlaceholder");

        public static string ZoneCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.ZoneCardTitle");

        public static string NoZones(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.NoZones");

        public static string ChooseZonePlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.ChooseZonePlaceholder");

        // Contains inline <a> markup — render via MarkupString.
        public static string NoServicesSynced(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.NoServicesSynced");

        public static string MustHaveCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.MustHaveCardTitle");

        public static string MustHaveUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.MustHaveUsage");

        public static string MustHavePlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.MustHavePlaceholder");

        public static string NiceToHaveCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.NiceToHaveCardTitle");

        public static string NiceToHaveUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.NiceToHaveUsage");

        public static string NiceToHavePlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.NiceToHavePlaceholder");

        public static string MutualExclusionNote(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServiceSelectionAdmin.MutualExclusionNote");
    }

    // AiChat's "Memories" extra admin page (MemoryAdmin.razor).
    public static class WebAiChatMemoryAdmin
    {
        public static string Heading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatMemoryAdmin.Heading");

        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatMemoryAdmin.Intro");

        public static string Empty(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatMemoryAdmin.Empty");

        // date is a pre-formatted invariant "yyyy-MM-dd" string — the plan keeps admin-grid
        // timestamps invariant, so this only localizes the surrounding words.
        public static string CreatedLine(Language lang, string date) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatMemoryAdmin.CreatedLine", ("date", date));

        public static string LastRecalled(Language lang, string date) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatMemoryAdmin.LastRecalled", ("date", date));

        public static string NeverRecalled(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatMemoryAdmin.NeverRecalled");

        public static string SalienceLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatMemoryAdmin.SalienceLabel");

        public static string ForgetButton(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatMemoryAdmin.ForgetButton");

        public static string SavedConfirm(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatMemoryAdmin.SavedConfirm");

        public static string Scope(Language lang, MemoryScope scope)
        {
            var key = $"Web.Editor.AiChatMemoryAdmin.Scope.{scope}";
            var label = MessageCatalog.Format(lang, key);
            return label == key ? scope.ToString() : label;
        }
    }

    // AiChat's "Health" extra admin page (AiChatHealth.razor).
    public static class WebAiChatHealth
    {
        public static string Heading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.Heading");

        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.Intro");

        public static string EmbeddingCoverageCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.EmbeddingCoverage.CardTitle");

        public static string ProviderHealthCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.ProviderHealth.CardTitle");

        public static string KnowledgeListenChannelsCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.KnowledgeListenChannels.CardTitle");

        public static string CoverageLine(Language lang, int embedded, int total, int percent) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.CoverageLine", ("embedded", embedded), ("total", total), ("percent", percent));

        public static string AwaitingEmbedding(Language lang, int count) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.AwaitingEmbedding", ("count", count));

        // Contains inline <strong> markup — render via MarkupString.
        public static string EmbeddingBackendLine(Language lang, string provider) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.EmbeddingBackendLine", ("provider", provider));

        public static string OllamaDefaultLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.OllamaDefaultLabel");

        public static string NoCallsRecorded(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.NoCallsRecorded");

        public static string ChatLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.ChatLabel");

        public static string EmbeddingsLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.EmbeddingsLabel");

        public static string NoDataBadge(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.NoDataBadge");

        public static string DegradedBadge(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.DegradedBadge");

        public static string HealthyBadge(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.HealthyBadge");

        // Contains inline <code> markup — render via MarkupString. model may be the literal "—"
        // placeholder (kept invariant — see the batch-4 localization notes).
        public static string ModelLine(Language lang, string model) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.ModelLine", ("model", model));

        // when is a pre-formatted invariant "yyyy-MM-dd HH:mm UTC" string, or the localized "never"
        // (Msg.WebCommon.AiHealthNever) — the plan keeps the timestamp itself invariant.
        public static string LastSuccessLine(Language lang, string when) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.LastSuccessLine", ("when", when));

        public static string LastErrorLine(Language lang, string when) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.LastErrorLine", ("when", when));

        public static string NoChannelsConfigured(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.NoChannelsConfigured");

        public static string TierColumn(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.TierColumn");

        public static string ChannelColumn(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.ChannelColumn");

        public static string AudienceColumn(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.AudienceColumn");

        public static string TierLabel(Language lang, GuildFeature tierFeature) => tierFeature switch
        {
            GuildFeature.AiChatKnowledgePreferred => MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.TierKnowledgePreferred"),
            GuildFeature.AiChatKnowledge => MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.TierKnowledgeNormal"),
            GuildFeature.AiChatKnowledgeLastResort => MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.TierKnowledgeLastResort"),
            _ => MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.TierListen"),
        };

        public static string CategorySuffix(Language lang, string name) =>
            MessageCatalog.Format(lang, "Web.Editor.AiChatHealth.CategorySuffix", ("name", name));
    }
}
