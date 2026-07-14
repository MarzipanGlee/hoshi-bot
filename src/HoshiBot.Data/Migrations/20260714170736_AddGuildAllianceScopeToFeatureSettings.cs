using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildAllianceScopeToFeatureSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GuildFeatureSettingTexts_GuildId_Feature_Audience_Key",
                table: "GuildFeatureSettingTexts");

            migrationBuilder.DropIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildId_Feature_Audience_Key",
                table: "GuildFeatureSettingSnowflakes");

            migrationBuilder.DropIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildId_Feature_Audience_Key_~",
                table: "GuildFeatureSettingSnowflakes");

            migrationBuilder.DropIndex(
                name: "IX_GuildEnabledFeatures_GuildId_Feature_Audience",
                table: "GuildEnabledFeatures");

            migrationBuilder.AddColumn<int>(
                name: "GuildAllianceId",
                table: "GuildFeatureSettingTexts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GuildAllianceId",
                table: "GuildFeatureSettingSnowflakes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GuildAllianceId",
                table: "GuildEnabledFeatures",
                type: "integer",
                nullable: true);

            // --- Data backfill for per-alliance feature scoping ---
            // Lost Falcons predates the GuildAlliance concept: it has Alliance-audience settings
            // but no link. Create its own-alliance link (LF / Lost Falcons, StfcAlliances.Id 7433
            // on Mindmeld/164) so those settings have a home — but only if that alliance is present
            // and it isn't already linked, so this is safe on any database. Roles mirror the ones
            // the guild already uses (see GuildSettingsSeedData).
            migrationBuilder.Sql("""
                INSERT INTO "GuildAlliances" ("GuildId", "StfcAllianceId", "MemberRoleId", "OfficerRoleId", "DiplomatRoleId")
                SELECT 793375182596866079, 7433, 793383681233518633, NULL, 829693359874375710
                WHERE EXISTS (SELECT 1 FROM "StfcAlliances" WHERE "Id" = 7433)
                  AND NOT EXISTS (SELECT 1 FROM "GuildAlliances" WHERE "GuildId" = 793375182596866079 AND "StfcAllianceId" = 7433);
                """);

            // Attach every existing Alliance-audience row (Audience = 1) to its guild's first-linked
            // (lowest-Id) alliance. Guilds with no link keep NULL and are adopted by the startup
            // self-heal (GuildAllianceService.AdoptAllOrphansAsync) once they link one — so nothing
            // is deleted or lost here.
            migrationBuilder.Sql("""
                UPDATE "GuildEnabledFeatures" f SET "GuildAllianceId" = ga."Id"
                FROM (SELECT DISTINCT ON ("GuildId") "Id", "GuildId" FROM "GuildAlliances" ORDER BY "GuildId", "Id") ga
                WHERE f."Audience" = 1 AND f."GuildAllianceId" IS NULL AND f."GuildId" = ga."GuildId";
                """);
            migrationBuilder.Sql("""
                UPDATE "GuildFeatureSettingSnowflakes" s SET "GuildAllianceId" = ga."Id"
                FROM (SELECT DISTINCT ON ("GuildId") "Id", "GuildId" FROM "GuildAlliances" ORDER BY "GuildId", "Id") ga
                WHERE s."Audience" = 1 AND s."GuildAllianceId" IS NULL AND s."GuildId" = ga."GuildId";
                """);
            migrationBuilder.Sql("""
                UPDATE "GuildFeatureSettingTexts" s SET "GuildAllianceId" = ga."Id"
                FROM (SELECT DISTINCT ON ("GuildId") "Id", "GuildId" FROM "GuildAlliances" ORDER BY "GuildId", "Id") ga
                WHERE s."Audience" = 1 AND s."GuildAllianceId" IS NULL AND s."GuildId" = ga."GuildId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingTexts_GuildAllianceId",
                table: "GuildFeatureSettingTexts",
                column: "GuildAllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingTexts_GuildId_Feature_Audience_Key_Guild~",
                table: "GuildFeatureSettingTexts",
                columns: new[] { "GuildId", "Feature", "Audience", "Key", "GuildAllianceId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildAllianceId",
                table: "GuildFeatureSettingSnowflakes",
                column: "GuildAllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildId_Feature_Audience_Key_~",
                table: "GuildFeatureSettingSnowflakes",
                columns: new[] { "GuildId", "Feature", "Audience", "Key", "GuildAllianceId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildId_Feature_Audience_Key~1",
                table: "GuildFeatureSettingSnowflakes",
                columns: new[] { "GuildId", "Feature", "Audience", "Key", "Value", "GuildAllianceId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_GuildEnabledFeatures_GuildAllianceId",
                table: "GuildEnabledFeatures",
                column: "GuildAllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildEnabledFeatures_GuildId_Feature_Audience_GuildAlliance~",
                table: "GuildEnabledFeatures",
                columns: new[] { "GuildId", "Feature", "Audience", "GuildAllianceId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildEnabledFeatures_GuildAlliances_GuildAllianceId",
                table: "GuildEnabledFeatures",
                column: "GuildAllianceId",
                principalTable: "GuildAlliances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildFeatureSettingSnowflakes_GuildAlliances_GuildAllianceId",
                table: "GuildFeatureSettingSnowflakes",
                column: "GuildAllianceId",
                principalTable: "GuildAlliances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildFeatureSettingTexts_GuildAlliances_GuildAllianceId",
                table: "GuildFeatureSettingTexts",
                column: "GuildAllianceId",
                principalTable: "GuildAlliances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuildEnabledFeatures_GuildAlliances_GuildAllianceId",
                table: "GuildEnabledFeatures");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildFeatureSettingSnowflakes_GuildAlliances_GuildAllianceId",
                table: "GuildFeatureSettingSnowflakes");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildFeatureSettingTexts_GuildAlliances_GuildAllianceId",
                table: "GuildFeatureSettingTexts");

            migrationBuilder.DropIndex(
                name: "IX_GuildFeatureSettingTexts_GuildAllianceId",
                table: "GuildFeatureSettingTexts");

            migrationBuilder.DropIndex(
                name: "IX_GuildFeatureSettingTexts_GuildId_Feature_Audience_Key_Guild~",
                table: "GuildFeatureSettingTexts");

            migrationBuilder.DropIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildAllianceId",
                table: "GuildFeatureSettingSnowflakes");

            migrationBuilder.DropIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildId_Feature_Audience_Key_~",
                table: "GuildFeatureSettingSnowflakes");

            migrationBuilder.DropIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildId_Feature_Audience_Key~1",
                table: "GuildFeatureSettingSnowflakes");

            migrationBuilder.DropIndex(
                name: "IX_GuildEnabledFeatures_GuildAllianceId",
                table: "GuildEnabledFeatures");

            migrationBuilder.DropIndex(
                name: "IX_GuildEnabledFeatures_GuildId_Feature_Audience_GuildAlliance~",
                table: "GuildEnabledFeatures");

            migrationBuilder.DropColumn(
                name: "GuildAllianceId",
                table: "GuildFeatureSettingTexts");

            migrationBuilder.DropColumn(
                name: "GuildAllianceId",
                table: "GuildFeatureSettingSnowflakes");

            migrationBuilder.DropColumn(
                name: "GuildAllianceId",
                table: "GuildEnabledFeatures");

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingTexts_GuildId_Feature_Audience_Key",
                table: "GuildFeatureSettingTexts",
                columns: new[] { "GuildId", "Feature", "Audience", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildId_Feature_Audience_Key",
                table: "GuildFeatureSettingSnowflakes",
                columns: new[] { "GuildId", "Feature", "Audience", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildId_Feature_Audience_Key_~",
                table: "GuildFeatureSettingSnowflakes",
                columns: new[] { "GuildId", "Feature", "Audience", "Key", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildEnabledFeatures_GuildId_Feature_Audience",
                table: "GuildEnabledFeatures",
                columns: new[] { "GuildId", "Feature", "Audience" },
                unique: true);
        }
    }
}
