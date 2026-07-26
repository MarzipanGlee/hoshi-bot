using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveAllianceSettingsToGuildAlliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add the new per-alliance columns (all nullable) BEFORE dropping the GuildSettings
            //    ones, so the values can be backfilled across.
            migrationBuilder.AddColumn<decimal>(
                name: "AllianceBoardingChannelId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BoardingRoleId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BotSupportChannelId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommandBridgeChannelId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommandBridgeMessageId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommandStaffJobsChannelId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultChannelCategoryId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FriendsCommandBridgeChannelId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FriendsCommandBridgeMessageId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemindersAlliesChannelId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemindersServicesChannelId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RulesDeChannelId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RulesEnChannelId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StaffCommandBridgeChannelId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StaffCommandBridgeMessageId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UserNotificationsChannelId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            // 2) Backfill each guild's single old GuildSettings value onto its PRIMARY (lowest-Id)
            //    linked alliance — mirrors the AddGuildAllianceScope precedent. Guilds with no link
            //    keep nothing (there was nowhere per-alliance to put it, and no consumer read it).
            migrationBuilder.Sql("""
                UPDATE "GuildAlliances" ga SET
                    "AllianceBoardingChannelId"     = gs."AllianceBoardingChannelId",
                    "BoardingRoleId"                = gs."BoardingRoleId",
                    "BotSupportChannelId"           = gs."BotSupportChannelId",
                    "CommandBridgeChannelId"        = gs."CommandBridgeChannelId",
                    "CommandBridgeMessageId"        = gs."CommandBridgeMessageId",
                    "CommandStaffJobsChannelId"     = gs."CommandStaffJobsChannelId",
                    "DefaultChannelCategoryId"      = gs."DefaultChannelCategoryId",
                    "FriendsCommandBridgeChannelId" = gs."FriendsCommandBridgeChannelId",
                    "FriendsCommandBridgeMessageId" = gs."FriendsCommandBridgeMessageId",
                    "RemindersAlliesChannelId"      = gs."RemindersAlliesChannelId",
                    "RemindersServicesChannelId"    = gs."RemindersServicesChannelId",
                    "RulesDeChannelId"              = gs."RulesDeChannelId",
                    "RulesEnChannelId"              = gs."RulesEnChannelId",
                    "StaffCommandBridgeChannelId"   = gs."StaffCommandBridgeChannelId",
                    "StaffCommandBridgeMessageId"   = gs."StaffCommandBridgeMessageId",
                    "UserNotificationsChannelId"    = gs."UserNotificationsChannelId"
                FROM "GuildSettings" gs
                WHERE ga."GuildId" = gs."GuildId"
                  AND ga."Id" = (SELECT MIN(ga2."Id") FROM "GuildAlliances" ga2 WHERE ga2."GuildId" = ga."GuildId");
                """);

            // MemberRoleId already exists on GuildAlliances (and the bot already reads it there) —
            // only fill the primary link's value from GuildSettings where the link has none yet, so
            // an existing per-alliance role is never overwritten.
            migrationBuilder.Sql("""
                UPDATE "GuildAlliances" ga SET "MemberRoleId" = gs."MemberRoleId"
                FROM "GuildSettings" gs
                WHERE ga."GuildId" = gs."GuildId"
                  AND ga."MemberRoleId" IS NULL
                  AND gs."MemberRoleId" IS NOT NULL
                  AND ga."Id" = (SELECT MIN(ga2."Id") FROM "GuildAlliances" ga2 WHERE ga2."GuildId" = ga."GuildId");
                """);

            // 3) Command Bridge republish queue gains its per-alliance dimension. The queue is
            //    ephemeral (drained every 15s); clear any in-flight rows so the new non-null FK has
            //    nothing to violate — a Publish can simply be re-triggered.
            migrationBuilder.Sql(@"DELETE FROM ""CommandBridgeRepublishRequests"";");

            migrationBuilder.AddColumn<int>(
                name: "GuildAllianceId",
                table: "CommandBridgeRepublishRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CommandBridgeRepublishRequests_GuildAllianceId",
                table: "CommandBridgeRepublishRequests",
                column: "GuildAllianceId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommandBridgeRepublishRequests_GuildAlliances_GuildAlliance~",
                table: "CommandBridgeRepublishRequests",
                column: "GuildAllianceId",
                principalTable: "GuildAlliances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // 4) Now that the values are backfilled, drop the old GuildSettings columns.
            migrationBuilder.DropColumn(
                name: "AllianceBoardingChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "BoardingRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "BotSupportChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "CommandBridgeChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "CommandBridgeMessageId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "CommandStaffJobsChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "DefaultChannelCategoryId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "FriendsCommandBridgeChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "FriendsCommandBridgeMessageId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "MemberRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "RemindersAlliesChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "RemindersServicesChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "RulesDeChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "RulesEnChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "StaffCommandBridgeChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "StaffCommandBridgeMessageId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "UserNotificationsChannelId",
                table: "GuildSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommandBridgeRepublishRequests_GuildAlliances_GuildAlliance~",
                table: "CommandBridgeRepublishRequests");

            migrationBuilder.DropIndex(
                name: "IX_CommandBridgeRepublishRequests_GuildAllianceId",
                table: "CommandBridgeRepublishRequests");

            migrationBuilder.DropColumn(
                name: "AllianceBoardingChannelId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "BoardingRoleId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "BotSupportChannelId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "CommandBridgeChannelId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "CommandBridgeMessageId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "CommandStaffJobsChannelId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "DefaultChannelCategoryId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "FriendsCommandBridgeChannelId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "FriendsCommandBridgeMessageId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "RemindersAlliesChannelId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "RemindersServicesChannelId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "RulesDeChannelId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "RulesEnChannelId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "StaffCommandBridgeChannelId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "StaffCommandBridgeMessageId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "UserNotificationsChannelId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "GuildAllianceId",
                table: "CommandBridgeRepublishRequests");

            migrationBuilder.AddColumn<decimal>(
                name: "AllianceBoardingChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BoardingRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BotSupportChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommandBridgeChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommandBridgeMessageId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommandStaffJobsChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultChannelCategoryId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FriendsCommandBridgeChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FriendsCommandBridgeMessageId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MemberRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemindersAlliesChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemindersServicesChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RulesDeChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RulesEnChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StaffCommandBridgeChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StaffCommandBridgeMessageId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UserNotificationsChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);
        }
    }
}
