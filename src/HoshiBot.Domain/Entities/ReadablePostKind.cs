namespace HoshiBot.Domain.Entities;

// What kind of post a read confirmation is attached to. The admin picks which of these carry the
// button (GuildFeature.ReadReceipts), so this is the vocabulary that choice is expressed in.
//
// Append-only: the value is stored on every ReadablePost row, and the setting keys are built from
// the member names.
public enum ReadablePostKind
{
    // Published by the Announcements feature from a staff draft.
    Announcement,

    // Posted by the Announcement Forwarder — somebody else's announcement, translated. By volume the
    // commonest readable post there is: 97 of them against 3 announcements when this was written.
    ForwardedAnnouncement,

    // The three below have no producer yet. They are declared now because the editor lists them, and
    // it says plainly that setting one has no effect until its feature exists — the alternative is
    // an admin switching something on and waiting for a post that can never arrive.
    DiplomacyPost,
    WelcomeMessage,
    AllianceRules,
}
