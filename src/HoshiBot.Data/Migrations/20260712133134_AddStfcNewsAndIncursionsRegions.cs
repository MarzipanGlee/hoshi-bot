using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStfcNewsAndIncursionsRegions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StfcEventStatuses",
                table: "StfcEventStatuses");

            // defaultValue: 0 below is only there to satisfy the NOT NULL add — every
            // pre-existing row would otherwise collide on Id = 0 the moment the primary key
            // is (re-)established. The UPDATE right after assigns each of the (at most 4,
            // known-by-name) pre-existing rows a distinct id before that happens; harmless
            // no-op on a fresh/empty database, where the seeder inserts everything instead.
            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "StfcEventStatuses",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "RegionId",
                table: "StfcEventStatuses",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "StfcEventStatuses" SET "Id" = CASE "EventGroup"
                    WHEN 'incursions' THEN 1
                    WHEN 'alliance_tournaments' THEN 2
                    WHEN 'sarris_invasions' THEN 3
                    WHEN 'flashpoint' THEN 4
                    ELSE "Id"
                END;
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StfcEventStatuses",
                table: "StfcEventStatuses",
                column: "Id");

            // Advance the identity sequence past any id just assigned above, so subsequent
            // inserts (including the regional incursions rows below) don't collide with them.
            // No-op on a fresh/empty table (MAX(Id) is null, coalesced to 1).
            migrationBuilder.Sql(
                """
                SELECT setval(pg_get_serial_sequence('"StfcEventStatuses"', 'Id'), COALESCE((SELECT MAX("Id") FROM "StfcEventStatuses"), 1));
                """);

            // Only an ALREADY-seeded database (production) has a pre-existing single
            // "incursions" row to fix — a fresh/empty database is left alone here and gets
            // all 6 correct rows (3 of them regional) from StfcEventStatusSeedData instead,
            // via SeedStfcEventStatusIfEmptyAsync. The old row's 23:00 UTC value was only
            // ever correct for APAC, silently wrong for US/EU guilds.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "StfcEventStatuses" WHERE "EventGroup" = 'incursions' AND "RegionId" IS NULL) THEN
                        DELETE FROM "StfcEventStatuses" WHERE "EventGroup" = 'incursions' AND "RegionId" IS NULL;
                        INSERT INTO "StfcEventStatuses" ("EventGroup", "RegionId", "EventStart", "EventEnd", "Active", "UpdatedAt", "NotifiedEventStart")
                        VALUES
                            ('incursions', 1, '2026-06-20T15:00:00Z', '2026-06-21T03:00:00Z', false, now(), '2026-06-20T15:00:00Z'),
                            ('incursions', 2, '2026-06-20T08:00:00Z', '2026-06-20T20:00:00Z', false, now(), '2026-06-20T08:00:00Z'),
                            ('incursions', 3, '2026-06-20T23:00:00Z', '2026-06-21T11:00:00Z', false, now(), '2026-06-20T23:00:00Z');
                    END IF;
                END $$;
                """);

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
                name: "IX_IncursionsRegionDefaults_RegionId",
                table: "IncursionsRegionDefaults",
                column: "RegionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcEventDateConfirmations_StfcNewsPostId_DiscordUserId",
                table: "StfcEventDateConfirmations",
                columns: new[] { "StfcNewsPostId", "DiscordUserId" },
                unique: true);

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
                name: "IX_TrustedUsers_DiscordUserId",
                table: "TrustedUsers",
                column: "DiscordUserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StfcEventStatuses_StfcRegions_RegionId",
                table: "StfcEventStatuses",
                column: "RegionId",
                principalTable: "StfcRegions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StfcEventStatuses_StfcRegions_RegionId",
                table: "StfcEventStatuses");

            migrationBuilder.DropTable(
                name: "IncursionsRegionDefaults");

            migrationBuilder.DropTable(
                name: "StfcEventDateConfirmations");

            migrationBuilder.DropTable(
                name: "StfcNewsPostGuildMessages");

            migrationBuilder.DropTable(
                name: "StfcNewsSettings");

            migrationBuilder.DropTable(
                name: "TrustedUsers");

            migrationBuilder.DropTable(
                name: "StfcNewsPosts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StfcEventStatuses",
                table: "StfcEventStatuses");

            migrationBuilder.DropIndex(
                name: "IX_StfcEventStatuses_EventGroup_RegionId",
                table: "StfcEventStatuses");

            migrationBuilder.DropIndex(
                name: "IX_StfcEventStatuses_RegionId",
                table: "StfcEventStatuses");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "StfcEventStatuses");

            migrationBuilder.DropColumn(
                name: "RegionId",
                table: "StfcEventStatuses");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StfcEventStatuses",
                table: "StfcEventStatuses",
                column: "EventGroup");
        }
    }
}
