using HoshiBot.Domain.Localization;

namespace HoshiBot.Domain.Entities;

// One posted Discord message that members can confirm they have read. Registered by whichever
// feature posted it (see ReadReceiptService.RegisterAsync), which is all that feature needs to know
// about read tracking.
//
// A registry rather than a column on Announcement: of the five kinds, three are not announcements
// and would have had to invent a Title, Severity and Attribution to fit that table.
public class ReadablePost
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong ChannelId { get; set; }

    public ulong MessageId { get; set; }

    public ReadablePostKind Kind { get; set; }

    // The scope this post was made for — which is the scope whose per-kind switch decided
    // ReadReceiptsEnabled below. Recorded for the same reason as that flag: so the answer stays
    // explainable later, and so an audit can tell WHY a given post carries a button.
    public GuildAudience Audience { get; set; }

    public int? GuildAllianceId { get; set; }

    // Whether this post carries a read-confirmation button — decided ONCE, when the post was made,
    // from the feature and the kind's switch.
    //
    // This flag is the whole reason changing the setting only affects new posts. Nothing downstream
    // re-derives it: the button, the unread list and the counter job all read it from here. Turning
    // a kind off therefore cannot retire buttons already in front of members or strand a post
    // half-confirmed, and turning one on cannot retroactively demand confirmation of something
    // nobody was asked about.
    public bool ReadReceiptsEnabled { get; set; }

    // Lets the refresh job skip a no-op editMessage when the count hasn't moved. Lives here rather
    // than on Announcement because it belongs to whatever actually carries the button.
    public int LastKnownReadCount { get; set; }

    // What to call this post in the unread list. Stored for the same reason as Language: the list
    // has to name posts of five different kinds, and asking each kind's own feature for a title
    // would put knowledge of all five back into the one place that was built not to need it.
    public string Title { get; set; } = "";

    // The language the post was rendered in. Stored rather than resolved later because the post's
    // own text is frozen in it — a button reading "Read receipt" under a German announcement would
    // be wrong even if the guild's language had since changed. It also keeps this table free of any
    // knowledge of where a given kind's scope language comes from.
    public Language Language { get; set; }

    // What the confirm button says, when the producing feature wants its own wording — Boarding lets
    // an admin write the caption. Null means the default for the kind.
    //
    // Stored for the same reason as Language: the button is re-rendered by the counter job and by the
    // unread list, neither of which knows which feature produced the post. Frozen on the row, they
    // cannot render the wrong caption; asked for at render time, they would each need to know every
    // kind's owner.
    public string? ButtonLabel { get; set; }

    public DateTimeOffset PostedAt { get; set; }

    public ICollection<ReadReceipt> Receipts { get; set; } = [];
}
