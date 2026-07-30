using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;

namespace HoshiBot.Web.Components.Shared;

public static class AudienceDisplay
{
    // One label per set flag, in the fixed Guild→Community order (Msg.WebAudience backs the
    // catalog lookup; single-flag callers take .First()).
    public static IEnumerable<string> Labels(Language language, GuildAudience audiences)
    {
        if (audiences.HasFlag(GuildAudience.Guild))
            yield return Msg.WebAudience.Label(language, GuildAudience.Guild);
        if (audiences.HasFlag(GuildAudience.Alliance))
            yield return Msg.WebAudience.Label(language, GuildAudience.Alliance);
        if (audiences.HasFlag(GuildAudience.Server))
            yield return Msg.WebAudience.Label(language, GuildAudience.Server);
        if (audiences.HasFlag(GuildAudience.VeilGroup))
            yield return Msg.WebAudience.Label(language, GuildAudience.VeilGroup);
        if (audiences.HasFlag(GuildAudience.Community))
            yield return Msg.WebAudience.Label(language, GuildAudience.Community);
    }

    // Shared between AudienceEditor's cards and (formerly) the plain-checkbox labels it replaced.
    // Msg.WebAudience.Description already returns "" for an audience without a catalog entry.
    public static string Description(Language language, GuildAudience audience) =>
        Msg.WebAudience.Description(language, audience);

    // Open Iconic class for a single audience flag — shared between the Features page's
    // section headers and the public landing page's audience overview cards, so both stay
    // in sync if an icon ever changes.
    public static string Icon(GuildAudience audience) => audience switch
    {
        GuildAudience.Guild => "oi-home",
        GuildAudience.Alliance => "oi-flag",
        GuildAudience.Server => "oi-hard-drive",
        GuildAudience.VeilGroup => "oi-layers",
        GuildAudience.Community => "oi-people",
        _ => "oi-tag",
    };

    // Kebab-case route segment for a single audience flag — the audience/alliance comes before
    // "features" in the path (e.g. manage/guild/{id}/server/features/{slug}), so all of an
    // alliance's pages share the /alliance/{id}/ prefix.
    public static string Slug(GuildAudience audience) => audience switch
    {
        GuildAudience.Guild => "guild",
        GuildAudience.Alliance => "alliance",
        GuildAudience.Server => "server",
        GuildAudience.VeilGroup => "veil-group",
        GuildAudience.Community => "community",
        _ => "",
    };

    // The route prefix for an audience's Feature pages: the catalog is prefix + "/features",
    // an editor is prefix + "/features/{featureSlug}". Guild is the bare guild path; Alliance
    // carries its id (or none, which the pages canonicalize to the primary alliance); every
    // other audience is prefixed by its slug. Single source of truth for the feature-URL shape,
    // shared by Features/Index, FeatureSettings and NavMenu.
    public static string FeatureScopePrefix(ulong guildId, GuildAudience audience, int? guildAllianceId) => audience switch
    {
        GuildAudience.Guild => $"manage/guild/{guildId}",
        GuildAudience.Alliance => guildAllianceId is { } id
            ? $"manage/guild/{guildId}/alliance/{id}"
            : $"manage/guild/{guildId}/alliance",
        _ => $"manage/guild/{guildId}/{Slug(audience)}",
    };

    // Ordinal-ignore-case since this parses a route segment a user may have typed/bookmarked.
    public static bool TryParseSlug(string? slug, out GuildAudience audience)
    {
        foreach (var candidate in Enum.GetValues<GuildAudience>())
        {
            if (candidate != GuildAudience.None && string.Equals(Slug(candidate), slug, StringComparison.OrdinalIgnoreCase))
            {
                audience = candidate;
                return true;
            }
        }

        audience = GuildAudience.None;
        return false;
    }
}
