using System.Text.Json.Serialization;

namespace HoshiBot.Web.Authorization;

// Shape of the entries returned by Discord's GET /users/@me/guilds, called with the
// logged-in user's own OAuth access token. "id" and "permissions" are numeric strings
// in Discord's API (they exceed safe JS integer precision), hence AllowReadingFromString.
public record DiscordUserGuild(
    [property: JsonPropertyName("id")] ulong Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("icon")] string? IconHash,
    [property: JsonPropertyName("owner")] bool Owner,
    [property: JsonPropertyName("permissions")] ulong Permissions);
