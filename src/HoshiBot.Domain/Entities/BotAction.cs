namespace HoshiBot.Domain.Entities;

// What the bot was trying to do when a Discord call failed, for the admin-facing report
// (NotificationDispatcher.NotifyAdminOfPermissionIssueAsync). One value per catch block that
// reports, replacing the per-feature Msg.*.Action* keys those blocks used to pass as free text.
//
// An enum rather than a string because it is also the throttle key: the old key was the *localized*
// action text, so two different channels failing the same action shared one hourly slot and only
// the first was ever reported — and changing the guild's language reset the throttle.
//
// Localized via Msg.Notify.Action(lang, action) → "Notify.Action.{value}". There is no compile-time
// check that a key exists for each value, so BotActionTests asserts it for both locales.
public enum BotAction
{
    /// NotificationDispatcher's own alert-channel fan-out.
    SendAlert,

    CreateTicketThread,
    SendTicketWelcome,
    AddTicketCommander,
    CloseTicket,

    CreateRoeForumPost,
    AddRoeThreadUsers,
    SendRoeThreadMessage,
    CloseRoeThread,

    /// The two AbsenceService report refreshes, split apart because they were the only call sites
    /// passing a parameterized action string — and therefore the only ones whose throttle key
    /// varied per invocation.
    RefreshAbsenceReport,
    RefreshStaffAbsenceReport,

    /// The read-counter button edit on a published announcement.
    UpdateAnnouncement,

    SendAnonymousMessage,

    /// Adding/removing an opt-in alert role from the Command Bridge.
    ToggleOptInRole,

    AdjustBetaTesterRole,

    /// ThreadCleanupJob deleting a queued thread.
    RemoveThread,

    /// Assigning or removing a member role — the role-sync jobs. Reported once per guild per run
    /// rather than per member: without Manage Roles, or with a target role sitting above the bot's
    /// own, every member in the roster would otherwise fail identically.
    SyncRoles,

    /// Renaming members to match their linked player (Nickname Sync).
    SyncNicknames,

    /// Decorating an announcement draft with the severity reactions — without them there is no way
    /// to publish at all, so a silent failure here is invisible until someone asks why.
    AddDraftReactions,
}
