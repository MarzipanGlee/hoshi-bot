using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscordGuilds",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NicknameSyncEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordGuilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscordUsers",
                columns: table => new
                {
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordUsers", x => x.DiscordUserId);
                });

            migrationBuilder.CreateTable(
                name: "GlobalAdmins",
                columns: table => new
                {
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalAdmins", x => x.DiscordUserId);
                });

            migrationBuilder.CreateTable(
                name: "StfcRegions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcRegions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StfcSystems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcSystems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    TargetDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    System = table.Column<string>(type: "text", nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    TriggeredByDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TerminatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TerminatedByDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerts_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildAdminRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildAdminRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildAdminRoles_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildAlertChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildAlertChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildAlertChannels_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildSettings",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    LogChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AdminChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserLogChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AbsencesReportChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AbsencesReportStaffChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AllianceBoardingChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AnnouncementsChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AnnouncementsRemindersChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CommandBridgeChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DiplomacyChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RaidReportsChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RemindersChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RemindersAlliesChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RemindersServicesChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RulesDeChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RulesEnChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RoeViolationsChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ShieldReminderChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserNotificationsChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AnonymousMessagesChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    BotSupportChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CommandStaffJobsChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CommandStaffRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CommodoresRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DiplomatRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    MemberRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    BoardingRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CrewsRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    BetaTesterRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    HoshiTesterRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AlertsRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    WarningsRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildSettings_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildTerritoryCaptureDayRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildTerritoryCaptureDayRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildTerritoryCaptureDayRoles_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationRoles_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThreadRemovalRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ThreadId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedByDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreadRemovalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThreadRemovalRequests_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildMembers",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildMembers", x => new { x.GuildId, x.DiscordUserId });
                    table.ForeignKey(
                        name: "FK_GuildMembers_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildMembers_DiscordUsers_DiscordUserId",
                        column: x => x.DiscordUserId,
                        principalTable: "DiscordUsers",
                        principalColumn: "DiscordUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcVeilGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RegionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcVeilGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcVeilGroups_StfcRegions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "StfcRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcTerritories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SystemId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcTerritories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcTerritories_StfcSystems_SystemId",
                        column: x => x.SystemId,
                        principalTable: "StfcSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AlertId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertNotifications_Alerts_AlertId",
                        column: x => x.AlertId,
                        principalTable: "Alerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Absences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SuppressNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Absences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Absences_GuildMembers_GuildId_DiscordUserId",
                        columns: x => new { x.GuildId, x.DiscordUserId },
                        principalTable: "GuildMembers",
                        principalColumns: new[] { "GuildId", "DiscordUserId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShieldReminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ShieldExpiration = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    System = table.Column<string>(type: "text", nullable: true),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShieldReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShieldReminders_GuildMembers_GuildId_DiscordUserId",
                        columns: x => new { x.GuildId, x.DiscordUserId },
                        principalTable: "GuildMembers",
                        principalColumns: new[] { "GuildId", "DiscordUserId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildVeilGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StfcVeilGroupId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildVeilGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildVeilGroups_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildVeilGroups_StfcVeilGroups_StfcVeilGroupId",
                        column: x => x.StfcVeilGroupId,
                        principalTable: "StfcVeilGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcServers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    RegionId = table.Column<int>(type: "integer", nullable: false),
                    VeilGroupId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcServers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcServers_StfcRegions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "StfcRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StfcServers_StfcVeilGroups_VeilGroupId",
                        column: x => x.VeilGroupId,
                        principalTable: "StfcVeilGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShieldReminderNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShieldReminderId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShieldReminderNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShieldReminderNotifications_ShieldReminders_ShieldReminderId",
                        column: x => x.ShieldReminderId,
                        principalTable: "ShieldReminders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildServers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StfcServerId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildServers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildServers_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildServers_StfcServers_StfcServerId",
                        column: x => x.StfcServerId,
                        principalTable: "StfcServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcAlliances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tag = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcAlliances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcAlliances_StfcServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "StfcServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildAlliances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StfcAllianceId = table.Column<int>(type: "integer", nullable: false),
                    MemberRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    OfficerRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DiplomatRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildAlliances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildAlliances_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildAlliances_StfcAlliances_StfcAllianceId",
                        column: x => x.StfcAllianceId,
                        principalTable: "StfcAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcAllianceDiplomacies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceAllianceId = table.Column<int>(type: "integer", nullable: false),
                    TargetAllianceId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcAllianceDiplomacies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcAllianceDiplomacies_StfcAlliances_SourceAllianceId",
                        column: x => x.SourceAllianceId,
                        principalTable: "StfcAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StfcAllianceDiplomacies_StfcAlliances_TargetAllianceId",
                        column: x => x.TargetAllianceId,
                        principalTable: "StfcAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StfcPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    AllianceId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcPlayers_StfcAlliances_AllianceId",
                        column: x => x.AllianceId,
                        principalTable: "StfcAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StfcPlayers_StfcServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "StfcServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcTerritoryOwnerships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TerritoryId = table.Column<int>(type: "integer", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    AllianceId = table.Column<int>(type: "integer", nullable: false),
                    LastCapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcTerritoryOwnerships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcTerritoryOwnerships_StfcAlliances_AllianceId",
                        column: x => x.AllianceId,
                        principalTable: "StfcAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StfcTerritoryOwnerships_StfcServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "StfcServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StfcTerritoryOwnerships_StfcTerritories_TerritoryId",
                        column: x => x.TerritoryId,
                        principalTable: "StfcTerritories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StfcPlayerId = table.Column<int>(type: "integer", nullable: false),
                    IsMain = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPlayers_DiscordUsers_DiscordUserId",
                        column: x => x.DiscordUserId,
                        principalTable: "DiscordUsers",
                        principalColumn: "DiscordUserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPlayers_StfcPlayers_StfcPlayerId",
                        column: x => x.StfcPlayerId,
                        principalTable: "StfcPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Absences_GuildId_DiscordUserId_EndsAt",
                table: "Absences",
                columns: new[] { "GuildId", "DiscordUserId", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertNotifications_AlertId",
                table: "AlertNotifications",
                column: "AlertId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_GuildId",
                table: "Alerts",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildAdminRoles_GuildId_DiscordRoleId",
                table: "GuildAdminRoles",
                columns: new[] { "GuildId", "DiscordRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildAlertChannels_GuildId",
                table: "GuildAlertChannels",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildAlliances_GuildId_StfcAllianceId",
                table: "GuildAlliances",
                columns: new[] { "GuildId", "StfcAllianceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildAlliances_StfcAllianceId",
                table: "GuildAlliances",
                column: "StfcAllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_DiscordUserId",
                table: "GuildMembers",
                column: "DiscordUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildServers_GuildId_StfcServerId",
                table: "GuildServers",
                columns: new[] { "GuildId", "StfcServerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildServers_StfcServerId",
                table: "GuildServers",
                column: "StfcServerId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildTerritoryCaptureDayRoles_GuildId_Day",
                table: "GuildTerritoryCaptureDayRoles",
                columns: new[] { "GuildId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildVeilGroups_GuildId_StfcVeilGroupId",
                table: "GuildVeilGroups",
                columns: new[] { "GuildId", "StfcVeilGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildVeilGroups_StfcVeilGroupId",
                table: "GuildVeilGroups",
                column: "StfcVeilGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRoles_GuildId_Kind",
                table: "NotificationRoles",
                columns: new[] { "GuildId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShieldReminderNotifications_ShieldReminderId",
                table: "ShieldReminderNotifications",
                column: "ShieldReminderId");

            migrationBuilder.CreateIndex(
                name: "IX_ShieldReminders_GuildId_DiscordUserId",
                table: "ShieldReminders",
                columns: new[] { "GuildId", "DiscordUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcAllianceDiplomacies_SourceAllianceId_TargetAllianceId",
                table: "StfcAllianceDiplomacies",
                columns: new[] { "SourceAllianceId", "TargetAllianceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcAllianceDiplomacies_TargetAllianceId",
                table: "StfcAllianceDiplomacies",
                column: "TargetAllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcAlliances_ServerId_Tag",
                table: "StfcAlliances",
                columns: new[] { "ServerId", "Tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcPlayers_AllianceId",
                table: "StfcPlayers",
                column: "AllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcPlayers_ServerId_Name",
                table: "StfcPlayers",
                columns: new[] { "ServerId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcRegions_Name",
                table: "StfcRegions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcServers_Number",
                table: "StfcServers",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcServers_RegionId",
                table: "StfcServers",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcServers_VeilGroupId_Name",
                table: "StfcServers",
                columns: new[] { "VeilGroupId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcSystems_Number",
                table: "StfcSystems",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritories_SystemId",
                table: "StfcTerritories",
                column: "SystemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryOwnerships_AllianceId",
                table: "StfcTerritoryOwnerships",
                column: "AllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryOwnerships_ServerId",
                table: "StfcTerritoryOwnerships",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryOwnerships_TerritoryId_ServerId",
                table: "StfcTerritoryOwnerships",
                columns: new[] { "TerritoryId", "ServerId" });

            migrationBuilder.CreateIndex(
                name: "IX_StfcVeilGroups_RegionId_Name",
                table: "StfcVeilGroups",
                columns: new[] { "RegionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThreadRemovalRequests_GuildId",
                table: "ThreadRemovalRequests",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ThreadRemovalRequests_RequestedAt",
                table: "ThreadRemovalRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserPlayers_DiscordUserId_StfcPlayerId",
                table: "UserPlayers",
                columns: new[] { "DiscordUserId", "StfcPlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPlayers_StfcPlayerId",
                table: "UserPlayers",
                column: "StfcPlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Absences");

            migrationBuilder.DropTable(
                name: "AlertNotifications");

            migrationBuilder.DropTable(
                name: "GlobalAdmins");

            migrationBuilder.DropTable(
                name: "GuildAdminRoles");

            migrationBuilder.DropTable(
                name: "GuildAlertChannels");

            migrationBuilder.DropTable(
                name: "GuildAlliances");

            migrationBuilder.DropTable(
                name: "GuildServers");

            migrationBuilder.DropTable(
                name: "GuildSettings");

            migrationBuilder.DropTable(
                name: "GuildTerritoryCaptureDayRoles");

            migrationBuilder.DropTable(
                name: "GuildVeilGroups");

            migrationBuilder.DropTable(
                name: "NotificationRoles");

            migrationBuilder.DropTable(
                name: "ShieldReminderNotifications");

            migrationBuilder.DropTable(
                name: "StfcAllianceDiplomacies");

            migrationBuilder.DropTable(
                name: "StfcTerritoryOwnerships");

            migrationBuilder.DropTable(
                name: "ThreadRemovalRequests");

            migrationBuilder.DropTable(
                name: "UserPlayers");

            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "ShieldReminders");

            migrationBuilder.DropTable(
                name: "StfcTerritories");

            migrationBuilder.DropTable(
                name: "StfcPlayers");

            migrationBuilder.DropTable(
                name: "GuildMembers");

            migrationBuilder.DropTable(
                name: "StfcSystems");

            migrationBuilder.DropTable(
                name: "StfcAlliances");

            migrationBuilder.DropTable(
                name: "DiscordGuilds");

            migrationBuilder.DropTable(
                name: "DiscordUsers");

            migrationBuilder.DropTable(
                name: "StfcServers");

            migrationBuilder.DropTable(
                name: "StfcVeilGroups");

            migrationBuilder.DropTable(
                name: "StfcRegions");
        }
    }
}
