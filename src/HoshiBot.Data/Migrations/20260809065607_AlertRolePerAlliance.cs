using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// The alert role moves to the alliance, and raid/shield channels stop carrying a role of their
    /// own — they ping whatever members can opt into, so the two could only ever disagree. On the
    /// test guild one raid channel already pinged a role nobody was offered.
    ///
    /// Alert-channel rows also gain the alliance they serve. They were audience-tagged only, which
    /// is why a coalition guild's five raid configurations all showed the same list, and why an
    /// Alliance row fell back to the guild language (the edge documented in NotificationDispatcher).
    ///
    /// The data move is ordered so nothing loses its ping: the opt-in setting wins where it exists,
    /// a single-alliance guild that never set one adopts what its channels already ping, and only
    /// then are rows tagged and their per-row roles cleared.
    /// </summary>
    public partial class AlertRolePerAlliance : Migration
    {
        // GuildFeature.NotificationOptIn and the Raid/Shield GuildAlertChannelKind values, pinned as
        // literals: both enums are append-only, and a migration must not shift meaning if one is
        // ever reordered by mistake.
        private const int NotificationOptInFeature = 8;
        private const string RaidAndShield = "(0, 1)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AlertRoleId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RoleId",
                table: "GuildAlertChannels",
                type: "numeric(20,0)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)");

            migrationBuilder.AddColumn<int>(
                name: "GuildAllianceId",
                table: "GuildAlertChannels",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildAlertChannels_GuildAllianceId",
                table: "GuildAlertChannels",
                column: "GuildAllianceId");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildAlertChannels_GuildAlliances_GuildAllianceId",
                table: "GuildAlertChannels",
                column: "GuildAllianceId",
                principalTable: "GuildAlliances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // 1. What members could actually toggle is authoritative — it is the role they hold.
            migrationBuilder.Sql($"""
                UPDATE "GuildAlliances" a
                SET "AlertRoleId" = s."Value"
                FROM "GuildFeatureSettingSnowflakes" s
                WHERE s."Feature" = {NotificationOptInFeature}
                  AND s."Key" = 'Role'
                  AND s."GuildAllianceId" = a."Id"
                  AND s."GuildId" = a."GuildId";
                """);

            migrationBuilder.Sql($"""
                DELETE FROM "GuildFeatureSettingSnowflakes"
                WHERE "Feature" = {NotificationOptInFeature} AND "Key" = 'Role';
                """);

            // 2. A guild that never configured the opt-in still has alerts going out. Where there is
            //    only one alliance the answer is unambiguous, so adopt the role its channels already
            //    ping (the most-used one) rather than leaving alerts with nobody to mention.
            migrationBuilder.Sql($"""
                UPDATE "GuildAlliances" a
                SET "AlertRoleId" = (
                    SELECT c."RoleId" FROM "GuildAlertChannels" c
                    WHERE c."GuildId" = a."GuildId" AND c."Kind" IN {RaidAndShield} AND c."RoleId" IS NOT NULL
                    GROUP BY c."RoleId"
                    ORDER BY count(*) DESC, c."RoleId"
                    LIMIT 1)
                WHERE a."AlertRoleId" IS NULL
                  AND (SELECT count(*) FROM "GuildAlliances" g WHERE g."GuildId" = a."GuildId") = 1;
                """);

            // 3. A row whose role already matches an alliance's alert role belongs to that alliance —
            //    the only evidence available, and exactly right for the rows that were configured
            //    consistently.
            migrationBuilder.Sql($"""
                UPDATE "GuildAlertChannels" c
                SET "GuildAllianceId" = a."Id"
                FROM "GuildAlliances" a
                WHERE c."Kind" IN {RaidAndShield}
                  AND c."GuildAllianceId" IS NULL
                  AND a."GuildId" = c."GuildId"
                  AND a."AlertRoleId" = c."RoleId";
                """);

            // 4. Whatever is left pinged a role no alliance claims — the mismatch this change exists
            //    to remove. It goes to the guild's primary alliance and will ping that alliance's
            //    role from now on, which is a real behaviour change for those channels.
            migrationBuilder.Sql($"""
                UPDATE "GuildAlertChannels" c
                SET "GuildAllianceId" = (SELECT min(a."Id") FROM "GuildAlliances" a WHERE a."GuildId" = c."GuildId")
                WHERE c."Kind" IN {RaidAndShield} AND c."GuildAllianceId" IS NULL;
                """);

            // 5. Raid/shield rows no longer answer the "which role" question.
            migrationBuilder.Sql($"""
                UPDATE "GuildAlertChannels" SET "RoleId" = NULL WHERE "Kind" IN {RaidAndShield};
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuildAlertChannels_GuildAlliances_GuildAllianceId",
                table: "GuildAlertChannels");

            migrationBuilder.DropIndex(
                name: "IX_GuildAlertChannels_GuildAllianceId",
                table: "GuildAlertChannels");

            migrationBuilder.DropColumn(
                name: "AlertRoleId",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "GuildAllianceId",
                table: "GuildAlertChannels");

            // Restore a role on the raid/shield rows first — the column goes back to NOT NULL, and
            // the alliance's role is the one they were pinging.
            migrationBuilder.Sql($"""
                UPDATE "GuildAlertChannels" c
                SET "RoleId" = COALESCE(a."AlertRoleId", 0)
                FROM "GuildAlliances" a
                WHERE c."GuildAllianceId" = a."Id" AND c."RoleId" IS NULL;
                """);

            migrationBuilder.Sql("""UPDATE "GuildAlertChannels" SET "RoleId" = 0 WHERE "RoleId" IS NULL;""");

            migrationBuilder.AlterColumn<decimal>(
                name: "RoleId",
                table: "GuildAlertChannels",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)",
                oldNullable: true);
        }
    }
}
