using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyFeatureSettingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbsencesReportChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "AbsencesReportStaffChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "AdmiralRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "AgentRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "AlertsRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "AnnouncementsChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "AnnouncementsDraftChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "AnnouncementsRemindersChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "AnonymousMessagesChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "CommodoreRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "DiplomacyChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "DiplomatRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "OperativeRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "PremierRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "RaidReportsChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "RoeViolationsChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "ShieldReminderChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "TerritoryCaptureInstructions",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "TicketsChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "ZoneSlot1RoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "ZoneSlot2RoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "ZoneSlot3RoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "ZoneSlot4RoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "ZoneSlot5RoleId",
                table: "GuildSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AbsencesReportChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AbsencesReportStaffChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdmiralRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AgentRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AlertsRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AnnouncementsChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AnnouncementsDraftChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AnnouncementsRemindersChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AnonymousMessagesChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommodoreRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiplomacyChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiplomatRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OperativeRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PremierRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RaidReportsChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RoeViolationsChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShieldReminderChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerritoryCaptureInstructions",
                table: "GuildSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TicketsChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZoneSlot1RoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZoneSlot2RoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZoneSlot3RoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZoneSlot4RoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZoneSlot5RoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);
        }
    }
}
