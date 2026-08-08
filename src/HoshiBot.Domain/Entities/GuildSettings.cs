namespace HoshiBot.Domain.Entities;

// One row per guild — global/wizard-level channel/role IDs not tied to one specific
// feature. Per-feature settings (Absences, Announcements, Diplomacy, Tickets, etc.) live in
// GuildFeatureSettingSnowflake/Text instead (see GuildFeatureSettingsService) — this entity
// only keeps: the audience selection itself, wizard/setup metadata, and channel/role IDs
// used across multiple features or not tied to any GuildFeature at all. Alert channel/role
// pairs (a genuinely multi-value setting) live in their own child table — see
// GuildAlertChannel.
public class GuildSettings
{
    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    // Which audience(s) this guild serves — set explicitly via the Setup Wizard's Audience
    // step or the Global Settings page, not derived from GuildAlliance/GuildServer/
    // GuildVeilGroup link existence (that was ambiguous: "hasn't set up Scope yet" vs.
    // "genuinely Community-only" looked identical). See GuildAudience for why this is UX
    // filtering only, never a runtime feature gate.
    public GuildAudience Audiences { get; set; }

    // Explicit guild bot-language (ISO 639-1 code). Null = derive from the guild's
    // Discord preferred_locale (DiscordGuild.PreferredLocale) — see
    // LanguagePolicy.ForGuild. Audience/alliance languages inherit from this.
    public string? Language { get; set; }

    // Channels. Alliance-scoped channels (Alliance Boarding, Reminders, Rules, User
    // Notifications, Bot Support, Senior Staff Jobs) and the Command Bridge channels/message
    // IDs moved to GuildAlliance so they can differ per linked alliance.
    public ulong? LogChannelId { get; set; }
    public ulong? AdminChannelId { get; set; }
    public ulong? UserLogChannelId { get; set; }

    public ulong? CrewsRoleId { get; set; }

    // Stamped when a guild admin completes (or explicitly finishes, even having skipped
    // steps) the Setup Wizard — drives the "needs setup" nudge on Guilds/Index.razor.
    public DateTimeOffset? SetupCompletedAt { get; set; }
}
