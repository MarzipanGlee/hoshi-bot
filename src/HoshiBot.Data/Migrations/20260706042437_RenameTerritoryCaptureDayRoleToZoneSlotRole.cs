using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    // Hand-edited: the scaffolded migration used DropTable+CreateTable, which would wipe
    // every guild's configured zone-slot roles on deploy. Rewritten as a true rename
    // (table, column, index, and both constraints) so existing rows and their IDs survive.
    public partial class RenameTerritoryCaptureDayRoleToZoneSlotRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"GuildTerritoryCaptureDayRoles\" RENAME CONSTRAINT \"PK_GuildTerritoryCaptureDayRoles\" TO \"PK_GuildTerritoryCaptureZoneSlotRoles\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"GuildTerritoryCaptureDayRoles\" RENAME CONSTRAINT \"FK_GuildTerritoryCaptureDayRoles_DiscordGuilds_GuildId\" TO \"FK_GuildTerritoryCaptureZoneSlotRoles_DiscordGuilds_GuildId\";");

            migrationBuilder.RenameTable(
                name: "GuildTerritoryCaptureDayRoles",
                newName: "GuildTerritoryCaptureZoneSlotRoles");

            migrationBuilder.RenameColumn(
                name: "Day",
                table: "GuildTerritoryCaptureZoneSlotRoles",
                newName: "SlotIndex");

            migrationBuilder.RenameIndex(
                name: "IX_GuildTerritoryCaptureDayRoles_GuildId_Day",
                table: "GuildTerritoryCaptureZoneSlotRoles",
                newName: "IX_GuildTerritoryCaptureZoneSlotRoles_GuildId_SlotIndex");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_GuildTerritoryCaptureZoneSlotRoles_GuildId_SlotIndex",
                table: "GuildTerritoryCaptureZoneSlotRoles",
                newName: "IX_GuildTerritoryCaptureDayRoles_GuildId_Day");

            migrationBuilder.RenameColumn(
                name: "SlotIndex",
                table: "GuildTerritoryCaptureZoneSlotRoles",
                newName: "Day");

            migrationBuilder.RenameTable(
                name: "GuildTerritoryCaptureZoneSlotRoles",
                newName: "GuildTerritoryCaptureDayRoles");

            migrationBuilder.Sql(
                "ALTER TABLE \"GuildTerritoryCaptureDayRoles\" RENAME CONSTRAINT \"FK_GuildTerritoryCaptureZoneSlotRoles_DiscordGuilds_GuildId\" TO \"FK_GuildTerritoryCaptureDayRoles_DiscordGuilds_GuildId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"GuildTerritoryCaptureDayRoles\" RENAME CONSTRAINT \"PK_GuildTerritoryCaptureZoneSlotRoles\" TO \"PK_GuildTerritoryCaptureDayRoles\";");
        }
    }
}
