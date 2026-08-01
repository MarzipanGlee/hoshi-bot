using System.Text.RegularExpressions;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain;

// Pure nickname composition for the Nickname Sync feature — no Discord/EF refs, same spirit as
// TerritoryCaptureScheduler. Lived inside NicknameSyncJob as a private static until the member
// suffix arrived; it's here now so the tag matrix, the truncation rule and the suffix behaviour are
// actually testable (the job keeps only the I/O around it).
public static partial class NicknameComposer
{
    public const int DiscordNicknameMaxLength = 32;

    // What a member may type on /me. Short by design: the tags already spend up to ~14 of the 32
    // characters, and a suffix that never fits would just silently never show.
    public const int MaxSuffixLength = 20;

    [GeneratedRegex(@"[\[\]()]")]
    private static partial Regex SuffixReservedChars();

    // Ported from legacy's $defs.RegEx.MemberName ("\[.*\]\s*"), applied to a guild nickname.
    [GeneratedRegex(@"\[.*\]\s*")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s*\([^()]*\)\s*$")]
    private static partial Regex SuffixPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // Normalizes what a member typed into what we're willing to render: brackets and parentheses go
    // (they'd confuse both CommanderName's tag strip and its suffix strip, which is what keeps
    // player auto-matching working), inner whitespace collapses, and the result is capped. Blank —
    // before or after cleaning — is null, i.e. "no suffix".
    public static string? CleanSuffix(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var cleaned = Whitespace().Replace(SuffixReservedChars().Replace(raw, ""), " ").Trim();
        if (cleaned.Length == 0)
            return null;

        return cleaned.Length > MaxSuffixLength ? cleaned[..MaxSuffixLength] : cleaned;
    }

    // "[SERVER][TAG] Player (Suffix)" — server tag, then alliance tag (no space between
    // them), the player name, then the member's own suffix in parentheses. Any part can be absent.
    public static string Build(
        string playerName,
        string regionName,
        int serverId,
        int? allianceId,
        string? allianceTag,
        NicknameTagMode allianceMode,
        NicknameTagMode serverMode,
        HashSet<int> homeAlliances,
        HashSet<int> homeServers,
        string? suffix = null)
    {
        var serverLabel = $"{regionName}{serverId}";
        var serverTag = Include(serverMode, serverId, homeServers) && !string.IsNullOrWhiteSpace(regionName) ? $"[{serverLabel}]" : "";

        // A player with no alliance is treated as "foreign" (not one of the guild's own) and, when the
        // tag applies, shown as [n/a] so it's still disambiguated.
        var showAllianceTag = allianceMode switch
        {
            NicknameTagMode.Always => true,
            NicknameTagMode.ForeignOnly => allianceId is not { } aid || !homeAlliances.Contains(aid),
            _ => false,
        };
        var allianceTagText = showAllianceTag ? $"[{(string.IsNullOrWhiteSpace(allianceTag) ? "n/a" : allianceTag)}]" : "";

        var prefix = serverTag + allianceTagText;
        var nickname = prefix.Length > 0 ? $"{prefix} {playerName}" : playerName;

        // The suffix is all-or-nothing: append it only if the whole thing still fits. Truncating
        // into it would leave a cut-off "(Suffi", and the tags+name are the part other systems
        // (CommanderName, the player matcher) read back out.
        if (CleanSuffix(suffix) is { } cleanSuffix)
        {
            var withSuffix = $"{nickname} ({cleanSuffix})";
            if (withSuffix.Length <= DiscordNicknameMaxLength)
                return withSuffix;
        }

        return nickname.Length > DiscordNicknameMaxLength ? nickname[..DiscordNicknameMaxLength] : nickname;
    }

    // The inverse of Build: peel the tags and the member suffix back off a nickname to get at the
    // bare player name. Lives next to Build because the two must agree — PlayerLinkService matches
    // members to STFC players by this output, so anything Build adds, this has to remove, or every
    // member using the feature drops out of auto-linking. Also what makes greetings read
    // "Commander Player" rather than "Commander [TAG] Player (Suffix)".
    //
    // The suffix strip does also remove a trailing parenthetical nobody asked us to touch (one a
    // member typed themselves, or an in-game name ending that way). That's the better trade: the
    // false positive is rare, the miss would be guaranteed for anyone using a suffix.
    public static string Strip(string nickname) =>
        SuffixPattern().Replace(TagPattern().Replace(nickname, ""), "");

    // Whether the server tag applies for this id under the given mode. ForeignOnly = the id is NOT one
    // of the guild's own servers.
    private static bool Include(NicknameTagMode mode, int id, HashSet<int> homeIds) => mode switch
    {
        NicknameTagMode.Always => true,
        NicknameTagMode.ForeignOnly => !homeIds.Contains(id),
        _ => false,
    };
}
