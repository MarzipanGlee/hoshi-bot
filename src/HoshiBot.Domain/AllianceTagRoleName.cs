namespace HoshiBot.Domain;

// Builds the Discord role name for an alliance tag, and the set of names that should be treated as
// "the role for this tag" when looking for one that already exists. Pure, so the sync job and the
// Web editor's preview can't disagree about what a given set of options produces.
public static class AllianceTagRoleName
{
    // Discord's limit for a role name.
    public const int MaxRoleNameLength = 100;

    public static string Build(string tag, bool latinize, bool lowercase, string? prefix, string? suffix)
    {
        var body = latinize ? AllianceTagLatinizer.ToLatin(tag) : tag;
        if (lowercase)
            body = body.ToLowerInvariant();

        var name = $"{prefix}{body}{suffix}";
        return name.Length > MaxRoleNameLength ? name[..MaxRoleNameLength] : name;
    }

    // Every name that counts as this tag's role. Both spellings are included regardless of the
    // Latinize option, so a guild that built its roles with it on and then turns it off (or the
    // reverse) reuses the roles it already has instead of creating a second set. Compare against
    // real role names with OrdinalIgnoreCase — assignment is always case-insensitive, so the
    // Lowercase option only ever decides how a NEW role gets named.
    public static IReadOnlyList<string> Candidates(string tag, bool latinize, bool lowercase, string? prefix, string? suffix)
    {
        var preferred = Build(tag, latinize, lowercase, prefix, suffix);
        var alternate = Build(tag, !latinize, lowercase, prefix, suffix);

        return string.Equals(preferred, alternate, StringComparison.OrdinalIgnoreCase)
            ? [preferred]
            : [preferred, alternate];
    }
}
