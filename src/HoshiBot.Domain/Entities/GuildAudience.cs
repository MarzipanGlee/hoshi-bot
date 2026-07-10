namespace HoshiBot.Domain.Entities;

// UX filtering only — decides what the Setup Wizard and Features page show by default, never
// blocks a GuildFeature from being enabled regardless of audience. A guild can be any
// combination (e.g. an alliance that also tracks a whole server), so this is a flags enum,
// not a mutually-exclusive choice. None means "hasn't been set yet" — treated the same as
// "show everything unfiltered", never as "show nothing".
[Flags]
public enum GuildAudience
{
    None = 0,
    Alliance = 1,
    Server = 2,
    Community = 4,
    VeilGroup = 8,
}
