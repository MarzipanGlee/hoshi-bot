namespace HoshiBot.Domain.Entities;

// The guild-specific Discord config for one shared alliance — the role-ID mappings (e.g.
// "which role marks our officers") plus the alliance-scoped channels and Command Bridge hub
// state. No diplomacy status here, that's StfcAllianceDiplomacy, an in-game fact independent
// of any guild. These are feature-agnostic per-alliance attributes; per-feature settings live
// in GuildFeatureSettingSnowflake/Text instead (see GuildFeatureSettingsService).
public class GuildAlliance
{
    // The alliance's home timezone, used to interpret local schedule times (e.g. the TC digest fire
    // times) DST-aware. Null → DefaultTimeZoneId. A general, feature-agnostic alliance attribute:
    // other schedule-driven features can reuse it rather than each carrying its own timezone.
    public const string DefaultTimeZoneId = "Europe/Zurich";

    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public int StfcAllianceId { get; set; }

    public StfcAlliance StfcAlliance { get; set; } = null!;

    // IANA timezone id (e.g. "Europe/Zurich") — see DefaultTimeZoneId. Null → default.
    public string? TimeZoneId { get; set; }

    // This alliance's bot language (ISO 639-1 code) for public alliance-scoped posts.
    // Null = inherit the guild language — see LanguagePolicy.ForAlliance. Same
    // feature-agnostic-attribute pattern as TimeZoneId.
    public string? Language { get; set; }

    // Roles. No Officer: it was a second name for the leadership role that nothing ever read, so it
    // merged into SeniorStaffRoleId below rather than staying as a picker admins had to guess about.
    public ulong? MemberRoleId { get; set; }
    public ulong? DiplomatRoleId { get; set; }
    public ulong? BoardingRoleId { get; set; }

    // Opt-in role pinged by anything alliance-wide worth interrupting people for: the absence-clean
    // notice, an elevated announcement, the weekly capture digest.
    //
    // Here rather than in a feature's settings because it is one role three features REACH FOR — it
    // belongs to the alliance, not to whichever feature happened to introduce it. It lived under
    // Absences and was editable from all three pages, which read as three settings that mysteriously
    // moved together and gave each page its own chance to name the role it might create.
    public ulong? NotificationRoleId { get; set; }

    // The opt-in role for this alliance's raid and shield alerts — the one members toggle in the
    // Notification Opt-In menu, and the one those alerts actually ping.
    //
    // One role, because it was two settings that had to agree by hand and silently didn't: the role
    // members could toggle lived in Notification Opt-In's settings, while the role an alert actually
    // pinged lived on each alert-channel row. A raid channel pointing at a third role meant members
    // opted into something that was never mentioned, with nothing anywhere to show the mismatch.
    public ulong? AlertRoleId { get; set; }

    // This alliance's senior staff — Star Trek's own word for a ship's leadership body (the CO plus
    // the senior crew holding positions of authority), which is exactly what this gates: reporting a
    // RoE violation on behalf of an own player, ending another commander's raid alert, confirming an
    // STFC News date, and the "im Auftrag von" attribution on its announcements.
    //
    // Named for the concept rather than a rank on purpose. "Officer" was the obvious alternative and
    // is wrong twice over: in canon it is a rank class, not a leadership body, and RANK is already
    // modelled — StfcPlayerRank's five in-game tiers (Admiral, Commodore, Premier, Operative, Agent)
    // drive the Rank Roles feature. Holding a tier and being allowed to act for the alliance are
    // different questions, so they get different words.
    //
    // Per alliance, not per guild: it was one GuildSettings value, so in a coalition guild every
    // alliance's leadership was the same role — LF's staff could end SHQL's raid alerts by virtue of
    // a setting neither of them chose.
    public ulong? SeniorStaffRoleId { get; set; }

    // Channels — alliance-scoped config formerly on GuildSettings (moved so a coalition guild
    // can configure each linked alliance independently). No boarding channel: it moved into the
    // Boarding feature's own settings when that feature was built, which is where it is read.
    public ulong? RemindersAlliesChannelId { get; set; }
    public ulong? RulesDeChannelId { get; set; }
    public ulong? RulesEnChannelId { get; set; }
    public ulong? UserNotificationsChannelId { get; set; }
    public ulong? SeniorStaffJobsChannelId { get; set; }

    // The category new channels get auto-created under (e.g. by the Setup Wizard) when none is
    // picked explicitly. Null covers both "never set" and an explicit "server root" choice.
    public ulong? DefaultChannelCategoryId { get; set; }

    // Command Bridge — one channel + one posted hub message id per bridge, so a (re)publish can
    // edit in place instead of re-posting a duplicate. Each linked alliance has its own bridges.
    public ulong? CommandBridgeChannelId { get; set; }
    public ulong? StaffCommandBridgeChannelId { get; set; }
    public ulong? FriendsCommandBridgeChannelId { get; set; }
    public ulong? CommandBridgeMessageId { get; set; }
    public ulong? StaffCommandBridgeMessageId { get; set; }
    public ulong? FriendsCommandBridgeMessageId { get; set; }

    // Resolves an IANA timezone id (a TimeZoneId value) to a TimeZoneInfo, falling back to
    // DefaultTimeZoneId for a null or unrecognized/invalid id — a stale id must never crash a
    // schedule or a prompt. Shared by the schedule-driven features that read TimeZoneId (the TC
    // digest; AiChat's current-date context).
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId ?? DefaultTimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
        }
    }
}
