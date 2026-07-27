namespace HoshiBot.Domain.Entities;

public class DiscordGuild
{
    public ulong Id { get; set; }

    public required string Name { get; set; }

    // Discord icon hash for the guild's server icon (null if the guild has no icon), kept in
    // sync by GuildSyncHandler on gateway connect. Lets the web guild picker render real
    // icons in support mode, where the admin sees guilds they aren't personally a member of
    // (so the OAuth /users/@me/guilds icon isn't available for them). Falls back to initials
    // when null. Animated icons carry an "a_" prefix, so this can exceed 32 chars.
    public string? IconHash { get; set; }

    // The guild's Discord preferred_locale, kept in sync by GuildSyncHandler like
    // Name/IconHash. The default source for the guild's bot language when no explicit
    // GuildSettings.Language is set — see LanguagePolicy.ForGuild.
    public string? PreferredLocale { get; set; }

    // A guild's scope is any combination of linked alliances/servers/veil-groups —
    // none of these are mutually exclusive (a coalition Discord can manage several
    // alliances; a whole-server community Discord may link a server but no single
    // alliance). A guild with none of these linked is a "global" guild. Alliance-specific
    // features (diplomacy, notification-role sync) only consume AllianceLinks for now.
    public ICollection<GuildAlliance> AllianceLinks { get; set; } = [];

    public ICollection<GuildServer> ServerLinks { get; set; } = [];

    public ICollection<GuildVeilGroup> VeilGroupLinks { get; set; } = [];

    public ICollection<GuildMember> Members { get; set; } = [];

    public ICollection<GuildAdminRole> AdminRoles { get; set; } = [];

    public ICollection<ThreadRemovalRequest> ThreadRemovalRequests { get; set; } = [];

    public ICollection<CommandBridgeRepublishRequest> CommandBridgeRepublishRequests { get; set; } = [];

    public ICollection<ChannelPermissionExpectation> ChannelPermissionExpectations { get; set; } = [];
}
