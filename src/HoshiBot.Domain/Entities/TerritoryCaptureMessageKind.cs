namespace HoshiBot.Domain.Entities;

// The three kinds of scheduled Territory Capture message the bot posts, each with its own
// retention (see TerritoryCaptureSentMessage.ExpiresAt): a per-capture "capture soon" ping
// (Single, removed when the capture ends), the "tomorrow's zones" evening digest (Daily,
// removed after a day) and the pinned weekly preview (Weekly, removed after a week).
public enum TerritoryCaptureMessageKind
{
    Single,
    Daily,
    Weekly,
}
