using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// Data-only: the post-capture "activate services" reminder moved out of Territory Capture into
    /// its own feature (GuildFeature.TerritoryCaptureServiceReminders). Its two snowflake settings
    /// (ServicesChannel, ServicesRole) were stored under TerritoryCapture, so they are re-keyed
    /// here, and the new feature is enabled wherever the reminder was actually live — otherwise
    /// every alliance that had one silently loses it, since a new feature starts disabled.
    ///
    /// Enum ordinals (pinned by the enum's own "keep last" comments): TerritoryCapture = 2,
    /// TerritoryCaptureServiceReminders = 31, GuildAudience.Alliance = 1.
    /// </summary>
    public partial class SplitTcServiceRemindersIntoFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable the new feature where the reminder actually fired: TC on and a ServicesChannel
            // configured (the job skips an alliance without one). Do this BEFORE re-keying the
            // settings, so the lookup still sees them under TerritoryCapture.
            migrationBuilder.Sql("""
                INSERT INTO "GuildEnabledFeatures" ("GuildId", "Feature", "Audience", "GuildAllianceId")
                SELECT DISTINCT tc."GuildId", 31, 1, tc."GuildAllianceId"
                FROM "GuildEnabledFeatures" tc
                JOIN "GuildFeatureSettingSnowflakes" s
                  ON s."GuildId" = tc."GuildId"
                 AND s."GuildAllianceId" = tc."GuildAllianceId"
                 AND s."Feature" = 2
                 AND s."Audience" = 1
                 AND s."Key" = 'ServicesChannel'
                WHERE tc."Feature" = 2
                  AND tc."Audience" = 1
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.Sql("""
                UPDATE "GuildFeatureSettingSnowflakes"
                SET "Feature" = 31
                WHERE "Feature" = 2 AND "Key" IN ('ServicesChannel', 'ServicesRole');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "GuildFeatureSettingSnowflakes"
                SET "Feature" = 2
                WHERE "Feature" = 31 AND "Key" IN ('ServicesChannel', 'ServicesRole');
                """);

            migrationBuilder.Sql("""
                DELETE FROM "GuildEnabledFeatures" WHERE "Feature" = 31;
                """);
        }
    }
}
