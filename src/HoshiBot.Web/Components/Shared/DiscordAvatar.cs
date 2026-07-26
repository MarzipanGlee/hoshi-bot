using System.Security.Claims;

namespace HoshiBot.Web.Components.Shared;

// The logged-in user's Discord avatar URL, built from the claims the OAuth handler stores.
// Shared by the admin topbar (MainLayout) and the /me index's identity header.
public static class DiscordAvatar
{
    // Null when the user has no custom avatar (or the claims aren't there) — callers render
    // their own fallback glyph rather than Discord's default-avatar endpoint.
    public static string? UrlFor(ClaimsPrincipal user)
    {
        var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var avatarHash = user.FindFirst("urn:discord:avatar:hash")?.Value;
        return id is not null && avatarHash is not null
            ? $"https://cdn.discordapp.com/avatars/{id}/{avatarHash}.png"
            : null;
    }
}
