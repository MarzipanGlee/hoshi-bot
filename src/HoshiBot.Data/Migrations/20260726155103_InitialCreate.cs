using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "DiscordGuilds",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IconHash = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    Locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
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
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SupportMode = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalAdmins", x => x.DiscordUserId);
                });

            migrationBuilder.CreateTable(
                name: "StfcClientReleases",
                columns: table => new
                {
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NotifiedVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcClientReleases", x => x.Platform);
                });

            migrationBuilder.CreateTable(
                name: "StfcNewsPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventGroup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubmittedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SubmittedByDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequiredConfirmations = table.Column<int>(type: "integer", nullable: false),
                    LastDisplayedConfirmationCount = table.Column<int>(type: "integer", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcNewsPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StfcNewsSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequiredConfirmationPercentage = table.Column<int>(type: "integer", nullable: false),
                    IncursionsEventDurationHours = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcNewsSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StfcRegions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcRegions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StfcTerritories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    Weekday = table.Column<int>(type: "integer", nullable: true),
                    CaptureTimeUtc = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcTerritories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StfcTerritoryServices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    LocaId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    InfoShort = table.Column<string>(type: "text", nullable: true),
                    Rarity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcTerritoryServices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TerritoryCaptureSentMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildAllianceId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    DedupKey = table.Column<string>(type: "text", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritoryCaptureSentMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TerritoryServiceSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TcSeason = table.Column<string>(type: "text", nullable: true),
                    GeneratedAt = table.Column<long>(type: "bigint", nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritoryServiceSyncStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrustedUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiChatBackfillStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    HistoryComplete = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiChatBackfillStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiChatBackfillStates_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiChatIndexedMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelName = table.Column<string>(type: "text", nullable: true),
                    AuthorName = table.Column<string>(type: "text", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IndexedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiChatIndexedMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiChatIndexedMessages_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiChatProviderHealths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiChatProviderHealths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiChatProviderHealths_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    MentionRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Attribution = table.Column<string>(type: "text", nullable: false),
                    AttachmentUrls = table.Column<string>(type: "text", nullable: false),
                    TriggeredByDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastKnownReadCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Announcements_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChannelPermissionExpectations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Allow = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Deny = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelPermissionExpectations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelPermissionExpectations_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ForwardedAnnouncements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SourceChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SourceMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DestinationChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DestinationMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SourceContentHash = table.Column<string>(type: "text", nullable: false),
                    ForwardedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForwardedAnnouncements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForwardedAnnouncements_DiscordGuilds_GuildId",
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
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false)
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
                name: "GuildFeatureChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Feature = table.Column<int>(type: "integer", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildFeatureChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildFeatureChannels_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildMemberNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PreferredName = table.Column<string>(type: "text", nullable: true),
                    Nicknames = table.Column<string>(type: "text", nullable: true),
                    Interests = table.Column<string>(type: "text", nullable: true),
                    Background = table.Column<string>(type: "text", nullable: true),
                    Languages = table.Column<string>(type: "text", nullable: true),
                    RunningJokes = table.Column<string>(type: "text", nullable: true),
                    TeaseAbout = table.Column<string>(type: "text", nullable: true),
                    PeerLoreHidden = table.Column<bool>(type: "boolean", nullable: false),
                    SelfUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PeerUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildMemberNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildMemberNotes_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildMemories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Salience = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastRecalledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SubjectDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    SubjectPersonKey = table.Column<string>(type: "text", nullable: true),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    SourceChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildMemories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildMemories_DiscordGuilds_GuildId",
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
                    Audiences = table.Column<int>(type: "integer", nullable: false),
                    LogChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AdminChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserLogChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CommandStaffRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CrewsRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    BetaTesterRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    HoshiTesterRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    SetupCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                name: "MemberInterviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildAllianceId = table.Column<int>(type: "integer", nullable: true),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DmChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: true),
                    InvitedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExtractedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberInterviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberInterviews_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PendingModalInputs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Field1 = table.Column<string>(type: "text", nullable: true),
                    Field2 = table.Column<string>(type: "text", nullable: true),
                    Field3 = table.Column<string>(type: "text", nullable: true),
                    Field4 = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingModalInputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingModalInputs_DiscordGuilds_GuildId",
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
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ThreadId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Subject = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OpenedByDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcEventDateConfirmations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StfcNewsPostId = table.Column<int>(type: "integer", nullable: false),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcEventDateConfirmations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcEventDateConfirmations_StfcNewsPosts_StfcNewsPostId",
                        column: x => x.StfcNewsPostId,
                        principalTable: "StfcNewsPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcNewsPostGuildMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StfcNewsPostId = table.Column<int>(type: "integer", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EligibleMemberCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcNewsPostGuildMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcNewsPostGuildMessages_StfcNewsPosts_StfcNewsPostId",
                        column: x => x.StfcNewsPostId,
                        principalTable: "StfcNewsPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncursionsRegionDefaults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RegionId = table.Column<int>(type: "integer", nullable: false),
                    DefaultStartTimeUtc = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncursionsRegionDefaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncursionsRegionDefaults_StfcRegions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "StfcRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcEventStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventGroup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RegionId = table.Column<int>(type: "integer", nullable: true),
                    EventStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotifiedEventStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcEventStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcEventStatuses_StfcRegions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "StfcRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcVeilGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
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
                name: "StfcSystems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HasStationHousing = table.Column<bool>(type: "boolean", nullable: false),
                    TerritoryId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcSystems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcSystems_StfcTerritories_TerritoryId",
                        column: x => x.TerritoryId,
                        principalTable: "StfcTerritories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StfcTerritoryNeighbours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TerritoryId = table.Column<int>(type: "integer", nullable: false),
                    NeighbourTerritoryId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcTerritoryNeighbours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcTerritoryNeighbours_StfcTerritories_NeighbourTerritoryId",
                        column: x => x.NeighbourTerritoryId,
                        principalTable: "StfcTerritories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StfcTerritoryNeighbours_StfcTerritories_TerritoryId",
                        column: x => x.TerritoryId,
                        principalTable: "StfcTerritories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberInterviewMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InterviewId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberInterviewMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberInterviewMessages_MemberInterviews_InterviewId",
                        column: x => x.InterviewId,
                        principalTable: "MemberInterviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberNoteSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TargetDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    TargetNameRaw = table.Column<string>(type: "text", nullable: false),
                    Field = table.Column<int>(type: "integer", nullable: false),
                    SuggestedText = table.Column<string>(type: "text", nullable: false),
                    SourceInterviewId = table.Column<int>(type: "integer", nullable: true),
                    SourceDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberNoteSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberNoteSuggestions_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberNoteSuggestions_MemberInterviews_SourceInterviewId",
                        column: x => x.SourceInterviewId,
                        principalTable: "MemberInterviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                name: "StfcVeilGroupDiscordInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VeilGroupId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcVeilGroupDiscordInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcVeilGroupDiscordInvites_StfcVeilGroups_VeilGroupId",
                        column: x => x.VeilGroupId,
                        principalTable: "StfcVeilGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    StfcSystemId = table.Column<int>(type: "integer", nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    Attacker = table.Column<string>(type: "text", nullable: true),
                    ServerLocation = table.Column<int>(type: "integer", nullable: false),
                    IsTest = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_Alerts_StfcSystems_StfcSystemId",
                        column: x => x.StfcSystemId,
                        principalTable: "StfcSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    ExternalId = table.Column<long>(type: "bigint", nullable: false),
                    Tag = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Emblem = table.Column<int>(type: "integer", nullable: true),
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
                name: "StfcServerDiscordInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcServerDiscordInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcServerDiscordInvites_StfcServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "StfcServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcServerStatuses",
                columns: table => new
                {
                    StfcServerId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Maintenance = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotifiedStatus = table.Column<int>(type: "integer", nullable: true),
                    NotifiedMaintenance = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcServerStatuses", x => x.StfcServerId);
                    table.ForeignKey(
                        name: "FK_StfcServerStatuses_StfcServers_StfcServerId",
                        column: x => x.StfcServerId,
                        principalTable: "StfcServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcTerritoryServiceSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    TerritoryId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcTerritoryServiceSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcTerritoryServiceSlots_StfcServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "StfcServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StfcTerritoryServiceSlots_StfcTerritories_TerritoryId",
                        column: x => x.TerritoryId,
                        principalTable: "StfcTerritories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StfcTerritoryServiceSlots_StfcTerritoryServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "StfcTerritoryServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "GuildAlliances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StfcAllianceId = table.Column<int>(type: "integer", nullable: false),
                    TimeZoneId = table.Column<string>(type: "text", nullable: true),
                    MemberRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    OfficerRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DiplomatRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    BoardingRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AllianceBoardingChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RemindersAlliesChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RulesDeChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    RulesEnChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    UserNotificationsChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    BotSupportChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CommandStaffJobsChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DefaultChannelCategoryId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CommandBridgeChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    StaffCommandBridgeChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    FriendsCommandBridgeChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CommandBridgeMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    StaffCommandBridgeMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    FriendsCommandBridgeMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
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
                name: "StfcAllianceDiscordInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AllianceId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcAllianceDiscordInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcAllianceDiscordInvites_StfcAlliances_AllianceId",
                        column: x => x.AllianceId,
                        principalTable: "StfcAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcAllianceNameHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StfcAllianceId = table.Column<int>(type: "integer", nullable: false),
                    Tag = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcAllianceNameHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcAllianceNameHistories_StfcAlliances_StfcAllianceId",
                        column: x => x.StfcAllianceId,
                        principalTable: "StfcAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExternalId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    AllianceId = table.Column<int>(type: "integer", nullable: true),
                    Rank = table.Column<int>(type: "integer", nullable: true),
                    OpsLevel = table.Column<int>(type: "integer", nullable: true)
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
                    AllianceId = table.Column<int>(type: "integer", nullable: false)
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
                name: "CommandBridgeRepublishRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildAllianceId = table.Column<int>(type: "integer", nullable: false),
                    Bridge = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandBridgeRepublishRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandBridgeRepublishRequests_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommandBridgeRepublishRequests_GuildAlliances_GuildAlliance~",
                        column: x => x.GuildAllianceId,
                        principalTable: "GuildAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildEnabledFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Feature = table.Column<int>(type: "integer", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    GuildAllianceId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildEnabledFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildEnabledFeatures_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildEnabledFeatures_GuildAlliances_GuildAllianceId",
                        column: x => x.GuildAllianceId,
                        principalTable: "GuildAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildFeatureSettingSnowflakes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Feature = table.Column<int>(type: "integer", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    GuildAllianceId = table.Column<int>(type: "integer", nullable: true),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildFeatureSettingSnowflakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildFeatureSettingSnowflakes_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildFeatureSettingSnowflakes_GuildAlliances_GuildAllianceId",
                        column: x => x.GuildAllianceId,
                        principalTable: "GuildAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildFeatureSettingTexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Feature = table.Column<int>(type: "integer", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    GuildAllianceId = table.Column<int>(type: "integer", nullable: true),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildFeatureSettingTexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildFeatureSettingTexts_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildFeatureSettingTexts_GuildAlliances_GuildAllianceId",
                        column: x => x.GuildAllianceId,
                        principalTable: "GuildAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoeViolationReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ThreadId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    AttackerAllianceTag = table.Column<string>(type: "text", nullable: false),
                    AttackerCommanderName = table.Column<string>(type: "text", nullable: false),
                    DefenderAllianceTag = table.Column<string>(type: "text", nullable: false),
                    DefenderCommanderName = table.Column<string>(type: "text", nullable: false),
                    ReportedByDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    GuildAllianceId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoeViolationReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoeViolationReports_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoeViolationReports_GuildAlliances_GuildAllianceId",
                        column: x => x.GuildAllianceId,
                        principalTable: "GuildAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TerritoryServiceSelections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildAllianceId = table.Column<int>(type: "integer", nullable: false),
                    TerritoryId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritoryServiceSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerritoryServiceSelections_GuildAlliances_GuildAllianceId",
                        column: x => x.GuildAllianceId,
                        principalTable: "GuildAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TerritoryServiceSelections_StfcTerritories_TerritoryId",
                        column: x => x.TerritoryId,
                        principalTable: "StfcTerritories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritoryServiceSelections_StfcTerritoryServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "StfcTerritoryServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GuildMembers",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PrimaryStfcPlayerId = table.Column<int>(type: "integer", nullable: true)
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
                    table.ForeignKey(
                        name: "FK_GuildMembers_StfcPlayers_PrimaryStfcPlayerId",
                        column: x => x.PrimaryStfcPlayerId,
                        principalTable: "StfcPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PlayerLinkReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildAllianceId = table.Column<int>(type: "integer", nullable: true),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Nickname = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CandidateStfcPlayerId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerLinkReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerLinkReviews_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerLinkReviews_StfcPlayers_CandidateStfcPlayerId",
                        column: x => x.CandidateStfcPlayerId,
                        principalTable: "StfcPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StfcPlayerNameHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StfcPlayerId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcPlayerNameHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcPlayerNameHistories_StfcPlayers_StfcPlayerId",
                        column: x => x.StfcPlayerId,
                        principalTable: "StfcPlayers",
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
                    StfcPlayerId = table.Column<int>(type: "integer", nullable: false)
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
                    CreatedByDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EditsAbsenceId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Absences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Absences_Absences_EditsAbsenceId",
                        column: x => x.EditsAbsenceId,
                        principalTable: "Absences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Absences_GuildMembers_GuildId_DiscordUserId",
                        columns: x => new { x.GuildId, x.DiscordUserId },
                        principalTable: "GuildMembers",
                        principalColumns: new[] { "GuildId", "DiscordUserId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementReadReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnnouncementId = table.Column<int>(type: "integer", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementReadReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementReadReceipts_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnnouncementReadReceipts_GuildMembers_GuildId_DiscordUserId",
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
                    StfcSystemId = table.Column<int>(type: "integer", nullable: true),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false),
                    Muted = table.Column<bool>(type: "boolean", nullable: false)
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
                    table.ForeignKey(
                        name: "FK_ShieldReminders_StfcSystems_StfcSystemId",
                        column: x => x.StfcSystemId,
                        principalTable: "StfcSystems",
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

            migrationBuilder.CreateIndex(
                name: "IX_Absences_EditsAbsenceId",
                table: "Absences",
                column: "EditsAbsenceId");

            migrationBuilder.CreateIndex(
                name: "IX_Absences_GuildId_DiscordUserId_EndsAt",
                table: "Absences",
                columns: new[] { "GuildId", "DiscordUserId", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiChatBackfillStates_GuildId",
                table: "AiChatBackfillStates",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_AiChatBackfillStates_GuildId_ChannelId",
                table: "AiChatBackfillStates",
                columns: new[] { "GuildId", "ChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiChatIndexedMessages_Embedding",
                table: "AiChatIndexedMessages",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_AiChatIndexedMessages_GuildId",
                table: "AiChatIndexedMessages",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_AiChatIndexedMessages_MessageId",
                table: "AiChatIndexedMessages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiChatProviderHealths_GuildId_Kind",
                table: "AiChatProviderHealths",
                columns: new[] { "GuildId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertNotifications_AlertId",
                table: "AlertNotifications",
                column: "AlertId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_GuildId",
                table: "Alerts",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_StfcSystemId",
                table: "Alerts",
                column: "StfcSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementReadReceipts_AnnouncementId_GuildId_DiscordUser~",
                table: "AnnouncementReadReceipts",
                columns: new[] { "AnnouncementId", "GuildId", "DiscordUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementReadReceipts_GuildId_DiscordUserId",
                table: "AnnouncementReadReceipts",
                columns: new[] { "GuildId", "DiscordUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_GuildId",
                table: "Announcements",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelPermissionExpectations_GuildId_ChannelId_RoleId",
                table: "ChannelPermissionExpectations",
                columns: new[] { "GuildId", "ChannelId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommandBridgeRepublishRequests_GuildAllianceId",
                table: "CommandBridgeRepublishRequests",
                column: "GuildAllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandBridgeRepublishRequests_GuildId",
                table: "CommandBridgeRepublishRequests",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandBridgeRepublishRequests_RequestedAt",
                table: "CommandBridgeRepublishRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardedAnnouncements_GuildId",
                table: "ForwardedAnnouncements",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardedAnnouncements_SourceMessageId",
                table: "ForwardedAnnouncements",
                column: "SourceMessageId",
                unique: true);

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
                name: "IX_GuildEnabledFeatures_GuildAllianceId",
                table: "GuildEnabledFeatures",
                column: "GuildAllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildEnabledFeatures_GuildId_Feature_Audience_GuildAlliance~",
                table: "GuildEnabledFeatures",
                columns: new[] { "GuildId", "Feature", "Audience", "GuildAllianceId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureChannels_GuildId_Feature_Audience_ChannelId",
                table: "GuildFeatureChannels",
                columns: new[] { "GuildId", "Feature", "Audience", "ChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildAllianceId",
                table: "GuildFeatureSettingSnowflakes",
                column: "GuildAllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildId_Feature_Audience_Key_~",
                table: "GuildFeatureSettingSnowflakes",
                columns: new[] { "GuildId", "Feature", "Audience", "Key", "GuildAllianceId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildId_Feature_Audience_Key~1",
                table: "GuildFeatureSettingSnowflakes",
                columns: new[] { "GuildId", "Feature", "Audience", "Key", "Value", "GuildAllianceId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingTexts_GuildAllianceId",
                table: "GuildFeatureSettingTexts",
                column: "GuildAllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingTexts_GuildId_Feature_Audience_Key_Guild~",
                table: "GuildFeatureSettingTexts",
                columns: new[] { "GuildId", "Feature", "Audience", "Key", "GuildAllianceId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_GuildMemberNotes_GuildId_DiscordUserId",
                table: "GuildMemberNotes",
                columns: new[] { "GuildId", "DiscordUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_DiscordUserId",
                table: "GuildMembers",
                column: "DiscordUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_PrimaryStfcPlayerId",
                table: "GuildMembers",
                column: "PrimaryStfcPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildMemories_GuildId_Scope",
                table: "GuildMemories",
                columns: new[] { "GuildId", "Scope" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildMemories_GuildId_SubjectPersonKey",
                table: "GuildMemories",
                columns: new[] { "GuildId", "SubjectPersonKey" });

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
                name: "IX_GuildVeilGroups_GuildId_StfcVeilGroupId",
                table: "GuildVeilGroups",
                columns: new[] { "GuildId", "StfcVeilGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildVeilGroups_StfcVeilGroupId",
                table: "GuildVeilGroups",
                column: "StfcVeilGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_IncursionsRegionDefaults_RegionId",
                table: "IncursionsRegionDefaults",
                column: "RegionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberInterviewMessages_InterviewId",
                table: "MemberInterviewMessages",
                column: "InterviewId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberInterviews_GuildId_DiscordUserId",
                table: "MemberInterviews",
                columns: new[] { "GuildId", "DiscordUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberNoteSuggestions_GuildId_Status",
                table: "MemberNoteSuggestions",
                columns: new[] { "GuildId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberNoteSuggestions_SourceInterviewId",
                table: "MemberNoteSuggestions",
                column: "SourceInterviewId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingModalInputs_GuildId",
                table: "PendingModalInputs",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerLinkReviews_CandidateStfcPlayerId",
                table: "PlayerLinkReviews",
                column: "CandidateStfcPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerLinkReviews_GuildId_DiscordUserId",
                table: "PlayerLinkReviews",
                columns: new[] { "GuildId", "DiscordUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoeViolationReports_GuildAllianceId",
                table: "RoeViolationReports",
                column: "GuildAllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_RoeViolationReports_GuildId",
                table: "RoeViolationReports",
                column: "GuildId");

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
                name: "IX_ShieldReminders_StfcSystemId",
                table: "ShieldReminders",
                column: "StfcSystemId");

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
                name: "IX_StfcAllianceDiscordInvites_AllianceId",
                table: "StfcAllianceDiscordInvites",
                column: "AllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcAllianceNameHistories_StfcAllianceId",
                table: "StfcAllianceNameHistories",
                column: "StfcAllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcAlliances_ExternalId",
                table: "StfcAlliances",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcAlliances_ServerId",
                table: "StfcAlliances",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcEventDateConfirmations_StfcNewsPostId_DiscordUserId",
                table: "StfcEventDateConfirmations",
                columns: new[] { "StfcNewsPostId", "DiscordUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcEventStatuses_EventGroup_RegionId",
                table: "StfcEventStatuses",
                columns: new[] { "EventGroup", "RegionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcEventStatuses_RegionId",
                table: "StfcEventStatuses",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcNewsPostGuildMessages_StfcNewsPostId_GuildId",
                table: "StfcNewsPostGuildMessages",
                columns: new[] { "StfcNewsPostId", "GuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcNewsPosts_Link",
                table: "StfcNewsPosts",
                column: "Link",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcPlayerNameHistories_StfcPlayerId",
                table: "StfcPlayerNameHistories",
                column: "StfcPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcPlayers_AllianceId",
                table: "StfcPlayers",
                column: "AllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcPlayers_ExternalId",
                table: "StfcPlayers",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcPlayers_ServerId",
                table: "StfcPlayers",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcRegions_Name",
                table: "StfcRegions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcServerDiscordInvites_ServerId",
                table: "StfcServerDiscordInvites",
                column: "ServerId");

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
                name: "IX_StfcSystems_TerritoryId",
                table: "StfcSystems",
                column: "TerritoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritories_Name",
                table: "StfcTerritories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryNeighbours_NeighbourTerritoryId",
                table: "StfcTerritoryNeighbours",
                column: "NeighbourTerritoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryNeighbours_TerritoryId_NeighbourTerritoryId",
                table: "StfcTerritoryNeighbours",
                columns: new[] { "TerritoryId", "NeighbourTerritoryId" },
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
                columns: new[] { "TerritoryId", "ServerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryServiceSlots_ServerId_TerritoryId",
                table: "StfcTerritoryServiceSlots",
                columns: new[] { "ServerId", "TerritoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryServiceSlots_ServerId_TerritoryId_ServiceId",
                table: "StfcTerritoryServiceSlots",
                columns: new[] { "ServerId", "TerritoryId", "ServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryServiceSlots_ServiceId",
                table: "StfcTerritoryServiceSlots",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryServiceSlots_TerritoryId",
                table: "StfcTerritoryServiceSlots",
                column: "TerritoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcVeilGroupDiscordInvites_VeilGroupId",
                table: "StfcVeilGroupDiscordInvites",
                column: "VeilGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcVeilGroups_RegionId_Name",
                table: "StfcVeilGroups",
                columns: new[] { "RegionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryCaptureSentMessages_GuildAllianceId_DedupKey",
                table: "TerritoryCaptureSentMessages",
                columns: new[] { "GuildAllianceId", "DedupKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryServiceSelections_GuildAllianceId_TerritoryId",
                table: "TerritoryServiceSelections",
                columns: new[] { "GuildAllianceId", "TerritoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryServiceSelections_GuildAllianceId_TerritoryId_Serv~",
                table: "TerritoryServiceSelections",
                columns: new[] { "GuildAllianceId", "TerritoryId", "ServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryServiceSelections_ServiceId",
                table: "TerritoryServiceSelections",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryServiceSelections_TerritoryId",
                table: "TerritoryServiceSelections",
                column: "TerritoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ThreadRemovalRequests_GuildId",
                table: "ThreadRemovalRequests",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ThreadRemovalRequests_RequestedAt",
                table: "ThreadRemovalRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_GuildId",
                table: "Tickets",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_TrustedUsers_DiscordUserId",
                table: "TrustedUsers",
                column: "DiscordUserId",
                unique: true);

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
                name: "AiChatBackfillStates");

            migrationBuilder.DropTable(
                name: "AiChatIndexedMessages");

            migrationBuilder.DropTable(
                name: "AiChatProviderHealths");

            migrationBuilder.DropTable(
                name: "AlertNotifications");

            migrationBuilder.DropTable(
                name: "AnnouncementReadReceipts");

            migrationBuilder.DropTable(
                name: "ChannelPermissionExpectations");

            migrationBuilder.DropTable(
                name: "CommandBridgeRepublishRequests");

            migrationBuilder.DropTable(
                name: "ForwardedAnnouncements");

            migrationBuilder.DropTable(
                name: "GlobalAdmins");

            migrationBuilder.DropTable(
                name: "GuildAdminRoles");

            migrationBuilder.DropTable(
                name: "GuildAlertChannels");

            migrationBuilder.DropTable(
                name: "GuildEnabledFeatures");

            migrationBuilder.DropTable(
                name: "GuildFeatureChannels");

            migrationBuilder.DropTable(
                name: "GuildFeatureSettingSnowflakes");

            migrationBuilder.DropTable(
                name: "GuildFeatureSettingTexts");

            migrationBuilder.DropTable(
                name: "GuildMemberNotes");

            migrationBuilder.DropTable(
                name: "GuildMemories");

            migrationBuilder.DropTable(
                name: "GuildServers");

            migrationBuilder.DropTable(
                name: "GuildSettings");

            migrationBuilder.DropTable(
                name: "GuildVeilGroups");

            migrationBuilder.DropTable(
                name: "IncursionsRegionDefaults");

            migrationBuilder.DropTable(
                name: "MemberInterviewMessages");

            migrationBuilder.DropTable(
                name: "MemberNoteSuggestions");

            migrationBuilder.DropTable(
                name: "PendingModalInputs");

            migrationBuilder.DropTable(
                name: "PlayerLinkReviews");

            migrationBuilder.DropTable(
                name: "RoeViolationReports");

            migrationBuilder.DropTable(
                name: "ShieldReminderNotifications");

            migrationBuilder.DropTable(
                name: "StfcAllianceDiplomacies");

            migrationBuilder.DropTable(
                name: "StfcAllianceDiscordInvites");

            migrationBuilder.DropTable(
                name: "StfcAllianceNameHistories");

            migrationBuilder.DropTable(
                name: "StfcClientReleases");

            migrationBuilder.DropTable(
                name: "StfcEventDateConfirmations");

            migrationBuilder.DropTable(
                name: "StfcEventStatuses");

            migrationBuilder.DropTable(
                name: "StfcNewsPostGuildMessages");

            migrationBuilder.DropTable(
                name: "StfcNewsSettings");

            migrationBuilder.DropTable(
                name: "StfcPlayerNameHistories");

            migrationBuilder.DropTable(
                name: "StfcServerDiscordInvites");

            migrationBuilder.DropTable(
                name: "StfcServerStatuses");

            migrationBuilder.DropTable(
                name: "StfcTerritoryNeighbours");

            migrationBuilder.DropTable(
                name: "StfcTerritoryOwnerships");

            migrationBuilder.DropTable(
                name: "StfcTerritoryServiceSlots");

            migrationBuilder.DropTable(
                name: "StfcVeilGroupDiscordInvites");

            migrationBuilder.DropTable(
                name: "TerritoryCaptureSentMessages");

            migrationBuilder.DropTable(
                name: "TerritoryServiceSelections");

            migrationBuilder.DropTable(
                name: "TerritoryServiceSyncStates");

            migrationBuilder.DropTable(
                name: "ThreadRemovalRequests");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "TrustedUsers");

            migrationBuilder.DropTable(
                name: "UserPlayers");

            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "MemberInterviews");

            migrationBuilder.DropTable(
                name: "ShieldReminders");

            migrationBuilder.DropTable(
                name: "StfcNewsPosts");

            migrationBuilder.DropTable(
                name: "GuildAlliances");

            migrationBuilder.DropTable(
                name: "StfcTerritoryServices");

            migrationBuilder.DropTable(
                name: "GuildMembers");

            migrationBuilder.DropTable(
                name: "StfcSystems");

            migrationBuilder.DropTable(
                name: "DiscordGuilds");

            migrationBuilder.DropTable(
                name: "DiscordUsers");

            migrationBuilder.DropTable(
                name: "StfcPlayers");

            migrationBuilder.DropTable(
                name: "StfcTerritories");

            migrationBuilder.DropTable(
                name: "StfcAlliances");

            migrationBuilder.DropTable(
                name: "StfcServers");

            migrationBuilder.DropTable(
                name: "StfcVeilGroups");

            migrationBuilder.DropTable(
                name: "StfcRegions");
        }
    }
}
