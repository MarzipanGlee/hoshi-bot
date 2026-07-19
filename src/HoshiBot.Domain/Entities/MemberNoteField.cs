namespace HoshiBot.Domain.Entities;

// Which GuildMemberNote field a MemberNoteSuggestion targets. Peers mostly contribute to the
// peer-authored fields (RunningJokes/TeaseAbout) and sometimes Interests; self-field values only
// become suggestions when extraction would otherwise clobber member-curated text.
public enum MemberNoteField
{
    PreferredName = 0,
    Nicknames = 1,
    Interests = 2,
    Background = 3,
    Languages = 4,
    RunningJokes = 5,
    TeaseAbout = 6,
}
