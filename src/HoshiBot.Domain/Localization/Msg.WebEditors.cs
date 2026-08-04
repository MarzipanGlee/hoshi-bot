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

    public static class WebAllianceTagRoles
    {
        // Contains inline markup — render via MarkupString.
        public static string HowItWorksUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.HowItWorksUsage");

        public static string CreateMissingLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.CreateMissingLabel");

        public static string CreateMissingUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.CreateMissingUsage");

        public static string LatinizeLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.LatinizeLabel");

        // Contains inline <code> markup (the examples) — render via MarkupString.
        public static string LatinizeUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.LatinizeUsage");

        public static string LowercaseLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.LowercaseLabel");

        public static string LowercaseUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.LowercaseUsage");

        public static string AffixUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.AffixUsage");

        public static string PrefixLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.PrefixLabel");

        public static string SuffixLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.SuffixLabel");

        public static string PreviewLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.PreviewLabel");

        public static string BoundRolesCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.BoundRolesCardTitle");

        public static string BoundRolesUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.BoundRolesUsage");

        public static string NoBoundRoles(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.AllianceTagRoles.NoBoundRoles");
    }

    public static class WebServerTagRoles
    {
        // Contains inline markup — render via MarkupString.
        public static string HowItWorksUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServerTagRoles.HowItWorksUsage");

        public static string ServersCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServerTagRoles.ServersCardTitle");

        public static string ServersUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServerTagRoles.ServersUsage");

        // Shown when the guild has linked no alliance, server or veil group yet, so there is
        // nothing to list.
        public static string NoServers(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServerTagRoles.NoServers");

        public static string ColServer(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServerTagRoles.ColServer");

        public static string ColRole(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ServerTagRoles.ColRole");
    }

    public static class WebConditionalRoles
    {
        // Contains inline markup — render via MarkupString.
        public static string HowItWorksUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.HowItWorksUsage");

        public static string RulesCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.RulesCardTitle");

        public static string RulesCardUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.RulesCardUsage");

        public static string RuleCount(Language lang, int enabled, int total) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.RuleCount", ("enabled", enabled), ("total", total));

        public static string OpenRulesLink(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.OpenRulesLink");

        public static string ConditionsCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.ConditionsCardTitle");

        public static string ConditionsCardUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.ConditionsCardUsage");

        public static string ConditionCount(Language lang, int count) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.ConditionCount", ("count", count));

        public static string OpenConditionsLink(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.OpenConditionsLink");

        public static string RulesIntro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.RulesIntro");

        public static string ConditionsIntro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.ConditionsIntro");

        public static string RuleNamePlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.RuleNamePlaceholder");

        public static string ConditionNamePlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.ConditionNamePlaceholder");

        public static string RuleEnabled(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.RuleEnabled");

        public static string TargetRoleLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.TargetRoleLabel");

        public static string ConditionLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.ConditionLabel");

        public static string AddRuleCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.AddRuleCardTitle");

        public static string AddConditionCardTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.AddConditionCardTitle");

        // The fail-closed state made visible: an unfinished rule grants nothing, and an admin should
        // read that here rather than infer it from ten minutes of nothing happening.
        public static string IncompleteWarning(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.IncompleteWarning");

        public static string ConditionIncompleteWarning(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.ConditionIncompleteWarning");

        public static string UnsavedChanges(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.UnsavedChanges");

        public static string CycleRejected(Language lang, string name) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.CycleRejected", ("name", name));

        public static string ConditionInUse(Language lang, string name, string usages) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.ConditionInUse", ("name", name), ("usages", usages));

        public static string KindAnd(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.KindAnd");

        public static string KindOr(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.KindOr");

        public static string KindNot(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.KindNot");

        public static string KindHasRole(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.KindHasRole");

        public static string KindMatchesCondition(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.KindMatchesCondition");

        public static string KindHasLinkedPlayer(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.KindHasLinkedPlayer");

        public static string KindInHomeAlliance(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.KindInHomeAlliance");

        public static string KindOnHomeServer(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.KindOnHomeServer");

        public static string KindIsPlayer(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.KindIsPlayer");

        public static string KindInAlliance(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.KindInAlliance");

        public static string ChoosePlayer(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.ChoosePlayer");

        public static string SearchPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.SearchPlaceholder");

        public static string ChooseOperand(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.ChooseOperand");

        public static string AddAllianceOption(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.AddAllianceOption");

        public static string AddPlayerOption(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.AddPlayerOption");

        // Explains the third outcome an admin can't see in the tree: a player fact is unanswerable
        // for a member with no linked player, so the rule leaves them alone rather than deciding.
        public static string PlayerFactsHint(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.PlayerFactsHint");

        public static string ChooseCondition(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.ChooseCondition");

        public static string AddRoleOption(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.AddRoleOption");

        public static string AddGroup(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.AddGroup");

        public static string AddConditionRef(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.AddConditionRef");

        public static string NotTakesOneHint(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.ConditionalRoles.NotTakesOneHint");
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

        public static string MemberSuffixLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.NicknameSync.MemberSuffixLabel");

        // Contains inline <code> markup (the rendered example) — render via MarkupString.
        public static string MemberSuffixUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.NicknameSync.MemberSuffixUsage");
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
        // Contains inline <strong>/<a> markup (the alliance timezone + Alliance Settings link) —
        // render via MarkupString. href is built by the caller from the current route.
        public static string DigestScheduleUsage(Language lang, string timezone, string href) =>
            MessageCatalog.Format(lang, "Web.Editor.TerritoryCapture.DigestScheduleUsage", ("timezone", timezone), ("href", href));

        public static string WeeklyDigestTimeLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.TerritoryCapture.WeeklyDigestTimeLabel");

        public static string DailyDigestTimeLabel(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.TerritoryCapture.DailyDigestTimeLabel");
    }

    // The post-capture "activate services" nudge, split out of TerritoryCapture into its own
    // feature (channel + role + the Service Selection page).
    public static class WebTerritoryCaptureServiceReminders
    {
        // Contains inline <strong> markup — render via MarkupString.
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.TerritoryCaptureServiceReminders.Intro");

        public static string ServiceSelectionLink(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.TerritoryCaptureServiceReminders.ServiceSelectionLink");
    }

    // The settings-free Capture Sign-Off feature — its editor is the enable switch plus this one
    // explanatory paragraph.
    public static class WebTerritoryCaptureSignOff
    {
        // Contains inline <strong> markup — render via MarkupString.
        public static string Intro(Language lang) =>
            MessageCatalog.Format(lang, "Web.Editor.TerritoryCaptureSignOff.Intro");
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
