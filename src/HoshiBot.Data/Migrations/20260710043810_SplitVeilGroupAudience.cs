using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitVeilGroupAudience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GuildAudience.ServerVeilGroup (2) is split into Server (2, unchanged bit) and
            // a new VeilGroup (8) — no column/type change, just a new flag value. Existing
            // data recorded under the old combined audience is best-effort backfilled onto
            // both new flags so nothing regresses to "not configured"; admins can uncheck
            // whichever doesn't apply via the Setup Wizard/Global Settings Audience step.

            // GuildSettings.Audiences is a real bitmask (a guild can be several audiences at
            // once) — just turn on the new bit alongside the old one.
            migrationBuilder.Sql(
                """
                UPDATE "GuildSettings" SET "Audiences" = "Audiences" | 8 WHERE ("Audiences" & 2) != 0;
                """);

            // GuildEnabledFeatures/GuildFeatureSettingSnowflakes/GuildAlertChannels store one
            // single Audience flag per row (never combined bits) — duplicate each
            // Audience=2 row onto Audience=8 so the feature/setting/channel stays available
            // under both new audiences.
            migrationBuilder.Sql(
                """
                INSERT INTO "GuildEnabledFeatures" ("GuildId", "Feature", "Audience")
                SELECT "GuildId", "Feature", 8
                FROM "GuildEnabledFeatures" e
                WHERE "Audience" = 2
                  AND NOT EXISTS (
                      SELECT 1 FROM "GuildEnabledFeatures" e2
                      WHERE e2."GuildId" = e."GuildId" AND e2."Feature" = e."Feature" AND e2."Audience" = 8
                  );
                """);
            migrationBuilder.Sql(
                """
                INSERT INTO "GuildFeatureSettingSnowflakes" ("GuildId", "Feature", "Audience", "Key", "Value")
                SELECT "GuildId", "Feature", 8, "Key", "Value"
                FROM "GuildFeatureSettingSnowflakes"
                WHERE "Audience" = 2;
                """);
            migrationBuilder.Sql(
                """
                INSERT INTO "GuildAlertChannels" ("GuildId", "Kind", "ChannelId", "RoleId", "Audience")
                SELECT "GuildId", "Kind", "ChannelId", "RoleId", 8
                FROM "GuildAlertChannels"
                WHERE "Audience" = 2;
                """);

            // GuildFeatureSettingTexts is Alliance-only in practice today (TerritoryCapture's
            // instructions text, the only feature that uses it) and Announcements/Tickets'
            // Audience columns are audit-only history, not live config — neither can hold a
            // pre-split ServerVeilGroup row worth backfilling.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "GuildAlertChannels" WHERE "Audience" = 8;
                """);
            migrationBuilder.Sql(
                """
                DELETE FROM "GuildFeatureSettingSnowflakes" WHERE "Audience" = 8;
                """);
            migrationBuilder.Sql(
                """
                DELETE FROM "GuildEnabledFeatures" WHERE "Audience" = 8;
                """);
            migrationBuilder.Sql(
                """
                UPDATE "GuildSettings" SET "Audiences" = "Audiences" & ~8 WHERE ("Audiences" & 8) != 0;
                """);
        }
    }
}
