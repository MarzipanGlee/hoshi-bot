using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

public class HoshiBotDbContext(DbContextOptions<HoshiBotDbContext> options) : DbContext(options)
{
    public DbSet<DiscordGuild> DiscordGuilds => Set<DiscordGuild>();

    public DbSet<StfcRegion> StfcRegions => Set<StfcRegion>();

    public DbSet<StfcVeilGroup> StfcVeilGroups => Set<StfcVeilGroup>();

    public DbSet<StfcServer> StfcServers => Set<StfcServer>();

    public DbSet<StfcServerDiscordInvite> StfcServerDiscordInvites => Set<StfcServerDiscordInvite>();

    public DbSet<StfcVeilGroupDiscordInvite> StfcVeilGroupDiscordInvites => Set<StfcVeilGroupDiscordInvite>();

    public DbSet<StfcAlliance> StfcAlliances => Set<StfcAlliance>();

    public DbSet<StfcAllianceDiscordInvite> StfcAllianceDiscordInvites => Set<StfcAllianceDiscordInvite>();

    public DbSet<StfcAllianceNameHistory> StfcAllianceNameHistories => Set<StfcAllianceNameHistory>();

    public DbSet<StfcPlayer> StfcPlayers => Set<StfcPlayer>();

    public DbSet<StfcPlayerNameHistory> StfcPlayerNameHistories => Set<StfcPlayerNameHistory>();

    public DbSet<StfcSystem> StfcSystems => Set<StfcSystem>();

    public DbSet<StfcTerritory> StfcTerritories => Set<StfcTerritory>();

    public DbSet<StfcTerritoryOwnership> StfcTerritoryOwnerships => Set<StfcTerritoryOwnership>();

    public DbSet<StfcTerritoryNeighbour> StfcTerritoryNeighbours => Set<StfcTerritoryNeighbour>();

    public DbSet<StfcTerritoryService> StfcTerritoryServices => Set<StfcTerritoryService>();

    public DbSet<StfcTerritoryServiceSlot> StfcTerritoryServiceSlots => Set<StfcTerritoryServiceSlot>();

    public DbSet<TerritoryServiceSyncState> TerritoryServiceSyncStates => Set<TerritoryServiceSyncState>();

    public DbSet<TerritoryServiceSelection> TerritoryServiceSelections => Set<TerritoryServiceSelection>();

    public DbSet<TerritoryCaptureSentMessage> TerritoryCaptureSentMessages => Set<TerritoryCaptureSentMessage>();

    public DbSet<StfcAllianceDiplomacy> StfcAllianceDiplomacies => Set<StfcAllianceDiplomacy>();

    public DbSet<DiscordUser> DiscordUsers => Set<DiscordUser>();

    public DbSet<UserPlayer> UserPlayers => Set<UserPlayer>();

    public DbSet<GuildMember> GuildMembers => Set<GuildMember>();

    public DbSet<GuildAlliance> GuildAlliances => Set<GuildAlliance>();

    public DbSet<GuildServer> GuildServers => Set<GuildServer>();

    public DbSet<GuildVeilGroup> GuildVeilGroups => Set<GuildVeilGroup>();

    public DbSet<GuildAdminRole> GuildAdminRoles => Set<GuildAdminRole>();

    public DbSet<GuildSettings> GuildSettings => Set<GuildSettings>();

    public DbSet<GuildAlertChannel> GuildAlertChannels => Set<GuildAlertChannel>();

    public DbSet<GlobalAdmin> GlobalAdmins => Set<GlobalAdmin>();

    public DbSet<Absence> Absences => Set<Absence>();

    public DbSet<ThreadRemovalRequest> ThreadRemovalRequests => Set<ThreadRemovalRequest>();

    public DbSet<CommandBridgeRepublishRequest> CommandBridgeRepublishRequests => Set<CommandBridgeRepublishRequest>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<AlertNotification> AlertNotifications => Set<AlertNotification>();

    public DbSet<ShieldReminder> ShieldReminders => Set<ShieldReminder>();

    public DbSet<ShieldReminderNotification> ShieldReminderNotifications => Set<ShieldReminderNotification>();

    public DbSet<Announcement> Announcements => Set<Announcement>();

    public DbSet<AnnouncementReadReceipt> AnnouncementReadReceipts => Set<AnnouncementReadReceipt>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<RoeViolationReport> RoeViolationReports => Set<RoeViolationReport>();

    public DbSet<PendingModalInput> PendingModalInputs => Set<PendingModalInput>();

    public DbSet<GuildEnabledFeature> GuildEnabledFeatures => Set<GuildEnabledFeature>();

    public DbSet<GuildFeatureSettingSnowflake> GuildFeatureSettingSnowflakes => Set<GuildFeatureSettingSnowflake>();

    public DbSet<GuildFeatureSettingText> GuildFeatureSettingTexts => Set<GuildFeatureSettingText>();

    public DbSet<GuildFeatureChannel> GuildFeatureChannels => Set<GuildFeatureChannel>();

    public DbSet<AiChatIndexedMessage> AiChatIndexedMessages => Set<AiChatIndexedMessage>();

    public DbSet<AiChatBackfillState> AiChatBackfillStates => Set<AiChatBackfillState>();

    public DbSet<MemberInterview> MemberInterviews => Set<MemberInterview>();

    public DbSet<MemberInterviewMessage> MemberInterviewMessages => Set<MemberInterviewMessage>();

    public DbSet<GuildMemberNote> GuildMemberNotes => Set<GuildMemberNote>();

    public DbSet<MemberNoteSuggestion> MemberNoteSuggestions => Set<MemberNoteSuggestion>();

    public DbSet<GuildMemory> GuildMemories => Set<GuildMemory>();

    public DbSet<AiChatProviderHealth> AiChatProviderHealths => Set<AiChatProviderHealth>();

    public DbSet<ForwardedAnnouncement> ForwardedAnnouncements => Set<ForwardedAnnouncement>();

    public DbSet<PlayerLinkReview> PlayerLinkReviews => Set<PlayerLinkReview>();

    public DbSet<ChannelPermissionExpectation> ChannelPermissionExpectations => Set<ChannelPermissionExpectation>();

    public DbSet<StfcServerStatus> StfcServerStatuses => Set<StfcServerStatus>();

    public DbSet<StfcEventStatus> StfcEventStatuses => Set<StfcEventStatus>();

    public DbSet<StfcNewsPost> StfcNewsPosts => Set<StfcNewsPost>();

    public DbSet<StfcNewsPostGuildMessage> StfcNewsPostGuildMessages => Set<StfcNewsPostGuildMessage>();

    public DbSet<StfcEventDateConfirmation> StfcEventDateConfirmations => Set<StfcEventDateConfirmation>();

    public DbSet<TrustedUser> TrustedUsers => Set<TrustedUser>();

    public DbSet<StfcNewsSettings> StfcNewsSettings => Set<StfcNewsSettings>();

    public DbSet<IncursionsRegionDefault> IncursionsRegionDefaults => Set<IncursionsRegionDefault>();

    public DbSet<StfcClientRelease> StfcClientReleases => Set<StfcClientRelease>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // pgvector: registers the `vector` extension so the AiChatIndexedMessage.Embedding column
        // (type vector(768)) and its distance operators are available (see AiChatEmbeddingService /
        // AiChatIndexService hybrid search). Requires a pgvector-capable Postgres image.
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HoshiBotDbContext).Assembly);
    }
}
