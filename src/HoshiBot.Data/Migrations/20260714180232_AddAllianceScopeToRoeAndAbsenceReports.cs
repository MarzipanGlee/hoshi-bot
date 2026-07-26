using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAllianceScopeToRoeAndAbsenceReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The absences "pinned report" message ids move to per-alliance settings keys
            // (AbsencesSettingKeys.ReportMessageId/ReportStaffMessageId). They are not data-migrated
            // here — the periodic refresh simply posts a fresh per-alliance report message on its
            // next run and records the new id, so at most one now-stale pinned message per guild
            // remains to unpin manually. (Migrating the old ids would mean hardcoding the Absences
            // enum ordinal in SQL, which is brittle.)
            migrationBuilder.DropColumn(
                name: "AbsencesReportMessageId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "AbsencesReportStaffMessageId",
                table: "GuildSettings");

            migrationBuilder.AddColumn<int>(
                name: "GuildAllianceId",
                table: "RoeViolationReports",
                type: "integer",
                nullable: true);

            // Attach existing reports to their guild's primary (lowest-Id) linked alliance so the
            // diplomat-ping on "ready for diplomat" resolves a role. Null stays null where a guild
            // has no link (SetReadyForDiplomat falls back gracefully).
            migrationBuilder.Sql("""
                UPDATE "RoeViolationReports" r SET "GuildAllianceId" = ga."Id"
                FROM (SELECT DISTINCT ON ("GuildId") "Id", "GuildId" FROM "GuildAlliances" ORDER BY "GuildId", "Id") ga
                WHERE r."GuildAllianceId" IS NULL AND r."GuildId" = ga."GuildId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RoeViolationReports_GuildAllianceId",
                table: "RoeViolationReports",
                column: "GuildAllianceId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoeViolationReports_GuildAlliances_GuildAllianceId",
                table: "RoeViolationReports",
                column: "GuildAllianceId",
                principalTable: "GuildAlliances",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoeViolationReports_GuildAlliances_GuildAllianceId",
                table: "RoeViolationReports");

            migrationBuilder.DropIndex(
                name: "IX_RoeViolationReports_GuildAllianceId",
                table: "RoeViolationReports");

            migrationBuilder.DropColumn(
                name: "GuildAllianceId",
                table: "RoeViolationReports");

            migrationBuilder.AddColumn<decimal>(
                name: "AbsencesReportMessageId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AbsencesReportStaffMessageId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);
        }
    }
}
