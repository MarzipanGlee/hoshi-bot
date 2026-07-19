namespace HoshiBot.Domain.Entities;

// Per-tag policy for the Nickname Sync feature — applied independently to the alliance tag and the
// server tag (see NicknameSyncSettingKeys.AllianceTagMode / ServerTagMode).
public enum NicknameTagMode
{
    Never,        // never include this tag
    ForeignOnly,  // include it only for a player outside the guild's own alliances/servers (to disambiguate)
    Always,       // always include it (when the player has the relevant alliance/server)
}
