using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// The Boarding feature: two new tables, the per-audience member/boarding roles, the button
    /// caption a producer can override, and the timestamp that tells Boarding which members predate
    /// it.
    ///
    /// Additive only — checked by hand, as the README requires. EnabledAt is deliberately nullable:
    /// rows written before it existed do not know when they were enabled, and a fabricated
    /// 0001-01-01 would read as a real cutoff and board an entire guild.
    /// </summary>
    public partial class BoardingFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ButtonLabel",
                table: "ReadablePosts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EnabledAt",
                table: "GuildEnabledFeatures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BoardingRoleId",
                table: "GuildAudienceSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MemberRoleId",
                table: "GuildAudienceSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BoardingEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ReadablePostId = table.Column<int>(type: "integer", nullable: false),
                    DmMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BoardedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardingEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardingEntries_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardingEntries_ReadablePosts_ReadablePostId",
                        column: x => x.ReadablePostId,
                        principalTable: "ReadablePosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardingRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    GuildAllianceId = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardingRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardingRequests_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardingRequests_GuildAlliances_GuildAllianceId",
                        column: x => x.GuildAllianceId,
                        principalTable: "GuildAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardingEntries_GuildId_DiscordUserId",
                table: "BoardingEntries",
                columns: new[] { "GuildId", "DiscordUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardingEntries_ReadablePostId",
                table: "BoardingEntries",
                column: "ReadablePostId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardingRequests_GuildAllianceId",
                table: "BoardingRequests",
                column: "GuildAllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardingRequests_GuildId",
                table: "BoardingRequests",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardingRequests_RequestedAt",
                table: "BoardingRequests",
                column: "RequestedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardingEntries");

            migrationBuilder.DropTable(
                name: "BoardingRequests");

            migrationBuilder.DropColumn(
                name: "ButtonLabel",
                table: "ReadablePosts");

            migrationBuilder.DropColumn(
                name: "EnabledAt",
                table: "GuildEnabledFeatures");

            migrationBuilder.DropColumn(
                name: "BoardingRoleId",
                table: "GuildAudienceSettings");

            migrationBuilder.DropColumn(
                name: "MemberRoleId",
                table: "GuildAudienceSettings");
        }
    }
}
