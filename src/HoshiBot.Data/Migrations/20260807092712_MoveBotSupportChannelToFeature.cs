using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// The bot-support channel moves off GuildAlliances.BotSupportChannelId and into the new
    /// GuildFeature.BotSupport's own settings, which is where a feature's configuration belongs.
    /// The column had no consumer at all — it held the channel legacy's Command Bridge pointed at,
    /// carried forward through the rewrite for a feature that hadn't been ported yet.
    ///
    /// EF scaffolded only the DropColumn, which would have discarded every configured value, so the
    /// copy and the enable are hand-written and must run before it.
    ///
    /// GuildFeature/GuildAudience are stored as their enum ordinals, which the enum's own comments
    /// pin ("keep last so existing enum ordinals/DB rows don't shift"): BotSupport = 35,
    /// GuildAudience.Alliance = 1.
    /// </summary>
    public partial class MoveBotSupportChannelToFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Carry each configured channel over to the feature's own setting.
            migrationBuilder.Sql("""
                INSERT INTO "GuildFeatureSettingSnowflakes" ("GuildId", "Feature", "Audience", "GuildAllianceId", "Key", "Value")
                SELECT a."GuildId", 35, 1, a."Id", 'Channel', a."BotSupportChannelId"
                FROM "GuildAlliances" a
                WHERE a."BotSupportChannelId" IS NOT NULL
                ON CONFLICT DO NOTHING;
                """);

            // An alliance that had picked a channel wanted the pointer, so turn the feature on for
            // exactly those — a new feature otherwise starts disabled and the setting would sit
            // there doing nothing, which is the state this whole change exists to end.
            migrationBuilder.Sql("""
                INSERT INTO "GuildEnabledFeatures" ("GuildId", "Feature", "Audience", "GuildAllianceId")
                SELECT a."GuildId", 35, 1, a."Id"
                FROM "GuildAlliances" a
                WHERE a."BotSupportChannelId" IS NOT NULL
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.DropColumn(
                name: "BotSupportChannelId",
                table: "GuildAlliances");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BotSupportChannelId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            // Put the values back where they came from, then clear what this migration created.
            migrationBuilder.Sql("""
                UPDATE "GuildAlliances" a
                SET "BotSupportChannelId" = s."Value"
                FROM "GuildFeatureSettingSnowflakes" s
                WHERE s."Feature" = 35 AND s."Audience" = 1 AND s."Key" = 'Channel'
                  AND s."GuildAllianceId" = a."Id";
                """);

            migrationBuilder.Sql("""
                DELETE FROM "GuildFeatureSettingSnowflakes" WHERE "Feature" = 35;
                DELETE FROM "GuildEnabledFeatures" WHERE "Feature" = 35;
                """);
        }
    }
}
