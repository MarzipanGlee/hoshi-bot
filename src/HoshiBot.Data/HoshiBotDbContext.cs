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

    public DbSet<StfcPlayer> StfcPlayers => Set<StfcPlayer>();

    public DbSet<StfcPlayerNameHistory> StfcPlayerNameHistories => Set<StfcPlayerNameHistory>();

    public DbSet<StfcSystem> StfcSystems => Set<StfcSystem>();

    public DbSet<StfcTerritory> StfcTerritories => Set<StfcTerritory>();

    public DbSet<StfcTerritoryOwnership> StfcTerritoryOwnerships => Set<StfcTerritoryOwnership>();

    public DbSet<StfcTerritoryNeighbour> StfcTerritoryNeighbours => Set<StfcTerritoryNeighbour>();

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

    public DbSet<NotificationRole> NotificationRoles => Set<NotificationRole>();

    public DbSet<Absence> Absences => Set<Absence>();

    public DbSet<ThreadRemovalRequest> ThreadRemovalRequests => Set<ThreadRemovalRequest>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<AlertNotification> AlertNotifications => Set<AlertNotification>();

    public DbSet<ShieldReminder> ShieldReminders => Set<ShieldReminder>();

    public DbSet<ShieldReminderNotification> ShieldReminderNotifications => Set<ShieldReminderNotification>();

    public DbSet<Announcement> Announcements => Set<Announcement>();

    public DbSet<AnnouncementReadReceipt> AnnouncementReadReceipts => Set<AnnouncementReadReceipt>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<RoeViolationReport> RoeViolationReports => Set<RoeViolationReport>();

    public DbSet<PendingModalInput> PendingModalInputs => Set<PendingModalInput>();

    public DbSet<GuildDisabledFeature> GuildDisabledFeatures => Set<GuildDisabledFeature>();

    public DbSet<ChannelPermissionExpectation> ChannelPermissionExpectations => Set<ChannelPermissionExpectation>();

    public DbSet<StfcServerStatus> StfcServerStatuses => Set<StfcServerStatus>();

    public DbSet<StfcEventStatus> StfcEventStatuses => Set<StfcEventStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HoshiBotDbContext).Assembly);
    }
}
