using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// Read tracking stops being an Announcements detail and becomes its own thing: any feature that
    /// posts something members should confirm registers a ReadablePost, and the receipts hang off
    /// that. Forwarded translations are the immediate reason — 97 of them against 3 announcements,
    /// none of them confirmable — with diplomacy posts, the welcome message and the alliance rules
    /// behind them.
    ///
    /// AnnouncementReadReceipts is dropped rather than migrated: it held 0 rows, so there is nothing
    /// to carry across, and reshaping it would have cost a join for data that does not exist.
    ///
    /// Enum ordinals are pinned by GuildFeature's own "keep last" comment: Announcements = 3,
    /// ReadReceipts = 37, GuildAudience.Guild = 16.
    /// </summary>
    public partial class ReadablePostRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReadablePosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ReadReceiptsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastKnownReadCount = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadablePosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadablePosts_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReadablePostId = table.Column<int>(type: "integer", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadReceipts_GuildMembers_GuildId_DiscordUserId",
                        columns: x => new { x.GuildId, x.DiscordUserId },
                        principalTable: "GuildMembers",
                        principalColumns: new[] { "GuildId", "DiscordUserId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadReceipts_ReadablePosts_ReadablePostId",
                        column: x => x.ReadablePostId,
                        principalTable: "ReadablePosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadablePosts_GuildId_MessageId",
                table: "ReadablePosts",
                columns: new[] { "GuildId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadablePosts_GuildId_ReadReceiptsEnabled_PostedAt",
                table: "ReadablePosts",
                columns: new[] { "GuildId", "ReadReceiptsEnabled", "PostedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadReceipts_GuildId_DiscordUserId",
                table: "ReadReceipts",
                columns: new[] { "GuildId", "DiscordUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadReceipts_ReadablePostId_DiscordUserId",
                table: "ReadReceipts",
                columns: new[] { "ReadablePostId", "DiscordUserId" },
                unique: true);

            // Every announcement already published becomes a tracked post, so nothing loses its ✅.
            //
            // LastKnownReadCount starts at -1, not 0. The count refresh job only edits a message when
            // the real count differs from this, and these posts have no receipts — at 0 it would
            // never fire, and their buttons still carry the old "announcement-read:{id}" custom id
            // that no longer has a handler. -1 guarantees exactly one repaint, which replaces them
            // with "read-receipt:{postId}".
            migrationBuilder.Sql("""
                INSERT INTO "ReadablePosts" ("GuildId", "ChannelId", "MessageId", "Kind", "ReadReceiptsEnabled", "LastKnownReadCount", "Title", "Language", "PostedAt")
                SELECT a."GuildId", a."ChannelId", a."MessageId", 0, true, -1, a."Title", 0, a."SentAt"
                FROM "Announcements" a
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.DropTable(
                name: "AnnouncementReadReceipts");

            migrationBuilder.DropColumn(
                name: "LastKnownReadCount",
                table: "Announcements");

            // Turn the new feature on, with the Announcement kind selected, wherever Announcements is
            // enabled — so a guild that had read confirmation yesterday still has it today. The
            // feature is guild-audience, hence the single row per guild regardless of how many
            // announcement audiences that guild uses.
            migrationBuilder.Sql("""
                INSERT INTO "GuildEnabledFeatures" ("GuildId", "Feature", "Audience", "GuildAllianceId")
                SELECT DISTINCT e."GuildId", 37, 16, NULL
                FROM "GuildEnabledFeatures" e
                WHERE e."Feature" = 3
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "GuildFeatureSettingTexts" ("GuildId", "Feature", "Audience", "GuildAllianceId", "Key", "Value")
                SELECT DISTINCT e."GuildId", 37, 16, NULL, 'Kind.Announcement', 'true'
                FROM "GuildEnabledFeatures" e
                WHERE e."Feature" = 3
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "GuildFeatureSettingTexts" WHERE "Feature" = 37;
                DELETE FROM "GuildEnabledFeatures" WHERE "Feature" = 37;
                """);

            migrationBuilder.DropTable(
                name: "ReadReceipts");

            migrationBuilder.DropTable(
                name: "ReadablePosts");

            migrationBuilder.AddColumn<int>(
                name: "LastKnownReadCount",
                table: "Announcements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementReadReceipts_AnnouncementId_GuildId_DiscordUser~",
                table: "AnnouncementReadReceipts",
                columns: new[] { "AnnouncementId", "GuildId", "DiscordUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementReadReceipts_GuildId_DiscordUserId",
                table: "AnnouncementReadReceipts",
                columns: new[] { "GuildId", "DiscordUserId" });
        }
    }
}
