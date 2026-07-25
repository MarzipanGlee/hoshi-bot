namespace HoshiBot.Domain.Entities;

// The four kinds of scheduled Territory Capture message the bot posts, each with its own
// retention (see TerritoryCaptureSentMessage.ExpiresAt): a per-capture "capture soon" ping
// (Single, removed when the capture ends), the "tomorrow's zones" evening digest (Daily,
// removed after a day), the pinned weekly preview (Weekly, removed after a week) and a
// post-capture "activate services" nudge for officers (Services, fired ~5 min after a
// capture ends, removed a few hours later). Append new kinds at the end — the ordinals are
// persisted as ints.
public enum TerritoryCaptureMessageKind
{
    Single,
    Daily,
    Weekly,
    Services,
}
