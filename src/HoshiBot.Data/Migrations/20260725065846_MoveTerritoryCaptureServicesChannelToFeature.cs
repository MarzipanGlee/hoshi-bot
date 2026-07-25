using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveTerritoryCaptureServicesChannelToFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Relocate the per-alliance services-reminder channel into the TerritoryCapture feature
            // settings (Feature = 2, Audience = 1 = Alliance) before dropping the column, mirroring
            // the earlier DigestChannel move (MoveRemindersChannelAndWarningsRoleToFeatures). The
            // column is already per-alliance, so it maps straight onto the same GuildAllianceId.
            migrationBuilder.Sql("""
                INSERT INTO "GuildFeatureSettingSnowflakes" ("GuildId", "Feature", "Audience", "GuildAllianceId", "Key", "Value")
                SELECT ga."GuildId", 2, 1, ga."Id", 'ServicesChannel', ga."RemindersServicesChannelId"
                FROM "GuildAlliances" ga
                WHERE ga."RemindersServicesChannelId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "GuildFeatureSettingSnowflakes" s
                     WHERE s."GuildId" = ga."GuildId" AND s."Feature" = 2 AND s."Audience" = 1
                       AND s."GuildAllianceId" = ga."Id" AND s."Key" = 'ServicesChannel');
                """);

            migrationBuilder.DropColumn(
                name: "RemindersServicesChannelId",
                table: "GuildAlliances");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RemindersServicesChannelId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);
        }
    }
}
