using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// Data-only: absence sign-off moved from a Territory Capture setting
    /// (TerritoryCaptureSettingKeys.AbsenceSignOff, default-ON) to its own settings-free feature
    /// (GuildFeature.TerritoryCaptureSignOff). Without this, every alliance that had sign-off
    /// silently loses it on deploy, since a new feature starts disabled for everyone.
    ///
    /// GuildFeature/GuildAudience are stored as their enum ordinals, which the enum's own comments
    /// pin ("keep last so existing enum ordinals/DB rows don't shift"): TerritoryCapture = 2,
    /// Absences = 7, TerritoryCaptureSignOff = 30, GuildAudience.Alliance = 1.
    /// </summary>
    public partial class MigrateTcSignOffSettingToFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable the new feature exactly where sign-off was actually live: Territory Capture on,
            // Absences on (the old runtime check required both), and the setting not explicitly
            // turned off. Unset counted as ON, hence the LEFT JOIN + IS NULL arm.
            migrationBuilder.Sql("""
                INSERT INTO "GuildEnabledFeatures" ("GuildId", "Feature", "Audience", "GuildAllianceId")
                SELECT tc."GuildId", 30, 1, tc."GuildAllianceId"
                FROM "GuildEnabledFeatures" tc
                JOIN "GuildEnabledFeatures" ab
                  ON ab."GuildId" = tc."GuildId"
                 AND ab."GuildAllianceId" = tc."GuildAllianceId"
                 AND ab."Feature" = 7
                 AND ab."Audience" = 1
                LEFT JOIN "GuildFeatureSettingTexts" s
                  ON s."GuildId" = tc."GuildId"
                 AND s."GuildAllianceId" = tc."GuildAllianceId"
                 AND s."Feature" = 2
                 AND s."Audience" = 1
                 AND s."Key" = 'AbsenceSignOff'
                WHERE tc."Feature" = 2
                  AND tc."Audience" = 1
                  AND (s."Value" IS NULL OR lower(s."Value") <> 'false')
                ON CONFLICT DO NOTHING;
                """);

            // The setting key no longer exists in code; drop its rows so nothing reads as configured.
            migrationBuilder.Sql("""
                DELETE FROM "GuildFeatureSettingTexts" WHERE "Feature" = 2 AND "Key" = 'AbsenceSignOff';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse of the insert. The deleted setting rows are not restored: the old default was
            // ON, so an absent row and the pre-migration state are equivalent for every alliance
            // that had the feature enabled here.
            migrationBuilder.Sql("""
                DELETE FROM "GuildEnabledFeatures" WHERE "Feature" = 30;
                """);
        }
    }
}
