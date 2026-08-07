namespace HoshiBot.Domain;

// The bot's whole symbol vocabulary, in one place. Every emoji Hoshi renders — Discord buttons and
// embeds, Command Bridge entries, the Web admin, and the localized message catalog — comes from
// here rather than from a literal at the call site.
//
// Why a registry: the same symbol was reaching users from a dozen unrelated files, and copies drift.
// The absence "notifications off" label wore a 🔔 for months; ⚠️ and ⚠ were used interchangeably
// across the bot and the Web; ✖️ appeared both with and without its variation selector. None of
// that is visible in a diff, and all of it is one edit away from fixed once the value has a name.
//
// Locale files reference these by name too — "{icon:Warning} …" — resolved by MessageCatalog, so a
// symbol is never duplicated across en/de. See MessageCatalog.ResolveIcon.
//
// Emoji vs. text presentation is a real distinction, not drift: Discord wants full-colour emoji,
// while the Web admin's dense tables want the monochrome glyphs in Icons.Text. Keep them apart.
public static class Icons
{
    // -- Status and results -------------------------------------------------------------------

    public const string Ok = "✅";
    public const string Error = "❌";
    public const string Warning = "⚠️";
    public const string Blocked = "🚫";
    public const string Pending = "⏳";

    public const string ServerUp = "🟢";
    public const string ServerDown = "🔴";
    public const string Maintenance = "🛠️";

    // -- Actions ------------------------------------------------------------------------------

    public const string Add = "➕";
    public const string Edit = "✏️";
    public const string Cancel = "✖️";
    public const string Back = "↩️";
    public const string Start = "▶️";
    public const string Stop = "⏹️";

    // -- Reminders and visibility -------------------------------------------------------------
    // Used for absence reminders, shield reminders and the alert opt-in alike.

    public const string RemindersOn = "🔔";
    public const string RemindersOff = "🔕";

    // Who can see something: everyone, or the command staff only.
    public const string Public = "📢";
    public const string StaffOnly = "🤐";

    // -- Discord channel kinds ----------------------------------------------------------------
    // Prefixed onto channel names in the Web pickers so a voice or forum channel is recognisable
    // in a flat <select>; text channels get Discord's own "#" instead.

    public const string VoiceChannel = "🔊";
    public const string ForumChannel = "📋";
    public const string Category = "📁";

    // -- Announcement severities --------------------------------------------------------------
    // Legacy's palette, and also the four reactions the bot adds to a draft — the emoji IS the
    // control, so changing one of these changes what staff have to click (AnnouncementSeverities).

    public const string SeverityNormal = "🟩";
    public const string SeverityElevated = "🟨";
    public const string SeverityHigh = "🟥";
    public const string SeverityDirect = "🟦";

    // -- Features and topics ------------------------------------------------------------------

    // Hoshi's own greeting — the Vulcan salute she signs outreach and persona lines with.
    public const string Hoshi = "🖖";
    public const string Smile = "😄";

    public const string News = "📰";
    public const string Date = "📅";
    public const string Alert = "🚨";
    public const string Reminder = "⏰";
    // The two halves of the Command Bridge's mail metaphor, kept deliberately distinct: an envelope
    // is a message going OUT to the command staff, a mailbox with its flag up is announcements
    // waiting to be read. They used to be 📮 and 📬 — two postboxes on one bridge.
    public const string ContactStaff = "✉️";
    public const string Unread = "📬";
    public const string Ticket = "🎟️";

    // The two help buttons on the user Command Bridge: 🆘 asks other members ("help with something
    // else" — the channel guide), ❓ asks about the bot itself (bot support).
    public const string ChannelGuide = "🆘";
    public const string Help = "❓";
    public const string Absence = "⛺";
    public const string MissingPlayers = "🕵️";
    public const string RoeViolation = "🚫";
    public const string Translation = "🌐";
    public const string Celebration = "🎉";
    public const string Tournament = "🏆";

    // Raid location, and the Incursions event: "at home" versus "on an enemy server".
    public const string HomeServer = "🏠";
    public const string EnemyServer = "⚔️";

    // Keycap digits for the per-zone Territory Capture buttons, matching legacy. Slot indices are
    // 1-5 in practice; the fallback only guards an unexpected value.
    public static string Keycap(int digit) => digit switch
    {
        0 => "0️⃣",
        1 => "1️⃣",
        2 => "2️⃣",
        3 => "3️⃣",
        4 => "4️⃣",
        5 => "5️⃣",
        6 => "6️⃣",
        7 => "7️⃣",
        8 => "8️⃣",
        9 => "9️⃣",
        10 => "🔟",
        _ => "🔢",
    };

    // Monochrome glyphs for the Web admin, where a full-colour emoji is too loud for a dense table
    // or an inline button. Deliberately text-presentation: no variation selector.
    public static class Text
    {
        public const string Check = "✓";
        public const string Cross = "✕";
        public const string Warning = "⚠";

        // The player link that represents a member in THIS guild (filled), versus one that doesn't
        // (hollow, and clickable to promote it).
        public const string Primary = "★";
        public const string NotPrimary = "☆";

        public const string Expanded = "▾";
        public const string Collapsed = "▸";
    }
}
