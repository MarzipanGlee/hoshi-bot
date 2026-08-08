using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// Command Staff becomes Senior Staff — Star Trek's own term for a ship's leadership body — and
    /// the unused Officer role, which was a second name for the same thing, goes away.
    ///
    /// HAND-WRITTEN. The scaffolded version paired the columns by shape rather than by name and got
    /// all three wrong: it renamed OfficerRoleId (the dead one) to SeniorStaffRoleId, renamed the
    /// real CommandStaffRoleId to SeniorStaffJobsChannelId — a role id landing in a channel column —
    /// and dropped CommandStaffJobsChannelId outright. Applied as scaffolded it would have promoted
    /// an unused role to gate RoE reports, raid termination and news confirmation, with no error.
    /// See README's migration warning.
    /// </summary>
    public partial class SeniorStaffRoleRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dropped first so the rename below has an unambiguous target. Nothing in the bot ever
            // read this column — it was ported config that no feature claimed.
            migrationBuilder.DropColumn(
                name: "OfficerRoleId",
                table: "GuildAlliances");

            migrationBuilder.RenameColumn(
                name: "CommandStaffRoleId",
                table: "GuildAlliances",
                newName: "SeniorStaffRoleId");

            migrationBuilder.RenameColumn(
                name: "CommandStaffJobsChannelId",
                table: "GuildAlliances",
                newName: "SeniorStaffJobsChannelId");

            migrationBuilder.RenameColumn(
                name: "CommandStaffRoleId",
                table: "GuildAudienceSettings",
                newName: "SeniorStaffRoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SeniorStaffRoleId",
                table: "GuildAudienceSettings",
                newName: "CommandStaffRoleId");

            migrationBuilder.RenameColumn(
                name: "SeniorStaffJobsChannelId",
                table: "GuildAlliances",
                newName: "CommandStaffJobsChannelId");

            migrationBuilder.RenameColumn(
                name: "SeniorStaffRoleId",
                table: "GuildAlliances",
                newName: "CommandStaffRoleId");

            // Comes back empty: the values were dropped going up, and nothing read them.
            migrationBuilder.AddColumn<decimal>(
                name: "OfficerRoleId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);
        }
    }
}
