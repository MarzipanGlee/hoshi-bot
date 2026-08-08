using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// The Command Staff role moves off GuildSettings and onto the scope it actually belongs to:
    /// GuildAlliance for the Alliance audience, GuildAudienceSettings for the others.
    ///
    /// It was one guild-wide role, but every feature that reads it is audience-scoped — so a
    /// coalition guild gave all its alliances the same leadership, and one alliance's staff could
    /// end another's raid alerts and sign another's announcements.
    ///
    /// GuildAudience: Alliance = 1, Server = 2, Community = 4, VeilGroup = 8.
    /// </summary>
    public partial class CommandStaffRolePerScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommandStaffRoleId",
                table: "GuildAudienceSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommandStaffRoleId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            // Every linked alliance inherits the guild's old role, so nothing changes for a
            // single-alliance guild and a coalition starts from what it already had rather than
            // from nothing.
            migrationBuilder.Sql("""
                UPDATE "GuildAlliances" a
                SET "CommandStaffRoleId" = s."CommandStaffRoleId"
                FROM "GuildSettings" s
                WHERE s."GuildId" = a."GuildId" AND s."CommandStaffRoleId" IS NOT NULL;
                """);

            // And each non-Alliance audience the guild actually serves — Announcements is the one
            // reader that reaches those, and leaving them empty would silently drop its attribution.
            migrationBuilder.Sql("""
                INSERT INTO "GuildAudienceSettings" ("GuildId", "Audience", "CommandStaffRoleId")
                SELECT s."GuildId", v.audience, s."CommandStaffRoleId"
                FROM "GuildSettings" s
                CROSS JOIN (VALUES (2), (4), (8)) AS v(audience)
                WHERE s."CommandStaffRoleId" IS NOT NULL
                  AND (s."Audiences" & v.audience) <> 0
                ON CONFLICT ("GuildId", "Audience")
                DO UPDATE SET "CommandStaffRoleId" = EXCLUDED."CommandStaffRoleId";
                """);

            migrationBuilder.DropColumn(
                name: "CommandStaffRoleId",
                table: "GuildSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommandStaffRoleId",
                table: "GuildAudienceSettings");

            migrationBuilder.DropColumn(
                name: "CommandStaffRoleId",
                table: "GuildAlliances");

            migrationBuilder.AddColumn<decimal>(
                name: "CommandStaffRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "GuildSettings" s
                SET "CommandStaffRoleId" = a."CommandStaffRoleId"
                FROM "GuildAlliances" a
                WHERE a."GuildId" = s."GuildId" AND a."CommandStaffRoleId" IS NOT NULL;
                """);
        }
    }
}
