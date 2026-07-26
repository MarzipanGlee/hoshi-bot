using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveNotificationRoleToAbsencesSetting : Migration
    {
        // Feature = GuildFeature.Absences (7), Audience = GuildAudience.Alliance (1),
        // NotificationRole Kind = General (0). The guild-wide notification role becomes a
        // per-alliance Absences setting — one row per linked alliance carrying the same role id.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "GuildFeatureSettingSnowflakes" ("GuildId", "Feature", "Audience", "GuildAllianceId", "Key", "Value")
                SELECT nr."GuildId", 7, 1, ga."Id", 'NotificationRole', nr."DiscordRoleId"
                FROM "NotificationRoles" nr
                JOIN "GuildAlliances" ga ON ga."GuildId" = nr."GuildId"
                WHERE nr."Kind" = 0
                  AND NOT EXISTS (
                    SELECT 1 FROM "GuildFeatureSettingSnowflakes" s
                    WHERE s."GuildId" = nr."GuildId" AND s."Feature" = 7 AND s."Audience" = 1
                      AND s."GuildAllianceId" = ga."Id" AND s."Key" = 'NotificationRole');
                """);

            migrationBuilder.DropTable(
                name: "NotificationRoles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRoles_GuildId_Kind",
                table: "NotificationRoles",
                columns: new[] { "GuildId", "Kind" },
                unique: true);

            // Reconstruct one guild-wide General role per guild from the per-alliance settings
            // (DISTINCT ON keeps the unique (GuildId, Kind) index happy if alliances diverged
            // after the migration), then drop the settings that replaced the table.
            migrationBuilder.Sql("""
                INSERT INTO "NotificationRoles" ("GuildId", "DiscordRoleId", "Kind")
                SELECT DISTINCT ON (s."GuildId") s."GuildId", s."Value", 0
                FROM "GuildFeatureSettingSnowflakes" s
                WHERE s."Feature" = 7 AND s."Audience" = 1 AND s."Key" = 'NotificationRole'
                ORDER BY s."GuildId", s."Id";

                DELETE FROM "GuildFeatureSettingSnowflakes"
                WHERE "Feature" = 7 AND "Audience" = 1 AND "Key" = 'NotificationRole';
                """);
        }
    }
}
