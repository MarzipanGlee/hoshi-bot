namespace HoshiBot.Web.Components.Shared;

// How a linked STFC player is written wherever one is listed — the admin assignment page and the
// member's own /me page. Same idea as CommanderName.Of on the Discord side: one place, so the two
// can't drift into showing the same player differently.
public static class PlayerLabel
{
    public static string Of(string name, string serverName, string? allianceTag) =>
        allianceTag is { Length: > 0 } ? $"{name} ({serverName}, [{allianceTag}])" : $"{name} ({serverName})";
}
