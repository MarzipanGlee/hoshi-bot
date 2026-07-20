namespace HoshiBot.Domain.Entities;

// What a GuildMemory is about — the three phases of Hoshi's memory share one store, differentiated
// by scope. See docs/ai-chat-member-lore.md / the memory plan.
public enum MemoryScope
{
    // Phase 1: a notable community event/happening, guild-wide ("die Allianz hat den Krieg gegen X gewonnen").
    Episodic = 0,

    // Phase 2: a rolling summary of a past conversation, keyed by ChannelId.
    Conversation = 1,

    // Phase 3: something remembered about an individual member, keyed by SubjectPersonKey/SubjectDiscordUserId.
    Member = 2,
}
