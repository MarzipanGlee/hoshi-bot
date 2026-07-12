namespace HoshiBot.Domain.Entities;

// Latest known STFC game client version per platform. Platform itself is the primary key — a
// small, fixed set, same reasoning StfcEventStatus/GlobalAdmin use elsewhere for a no-surrogate-
// Id key. Version is what's currently observed; NotifiedVersion is what was last actually
// announced to Discord — kept separate so StfcClientReleaseNotifyJob can just diff the two,
// same split StfcServerStatus uses. Unlike StfcServerStatus/StfcEventStatus, this is genuinely
// live from day one (no seed data) — all 3 implemented sources (Xsolla, Play Store, iTunes
// Lookup API) are real, working, permitted endpoints, so the job populates its own baseline row
// per platform on first run.
public class StfcClientRelease
{
    public StfcClientPlatform Platform { get; set; }

    public required string Version { get; set; }

    public string? NotifiedVersion { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
