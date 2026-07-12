namespace HoshiBot.Domain.Entities;

// One value per STFC game client platform tracked for new-version announcements. Linux has no
// known version-check source (ported from the legacy PHP bot, which had the same gap) — kept
// as a defined case for parity/future-proofing, not acted on anywhere.
public enum StfcClientPlatform
{
    Windows,
    MacOS,
    Linux,
    Android,
    IOS,
}
