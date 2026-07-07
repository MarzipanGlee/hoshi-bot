namespace HoshiBot.Domain.Entities;

public class DiscordGuild
{
    public ulong Id { get; set; }

    public required string Name { get; set; }

    public string Locale { get; set; } = "de";

    // Per-guild opt-in for NicknameSyncJob; off by default. /link-player still
    // works regardless of this flag — it only gates the automatic renaming job.
    public bool NicknameSyncEnabled { get; set; }

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

    public ICollection<NotificationRole> NotificationRoles { get; set; } = [];

    public ICollection<ThreadRemovalRequest> ThreadRemovalRequests { get; set; } = [];
}
