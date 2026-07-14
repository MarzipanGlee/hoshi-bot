using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveRemindersChannelAndWarningsRoleToFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Relocate the two guild-level values into per-alliance feature settings (Audience = 1
            // = Alliance) before dropping the columns. Copied onto every linked alliance of the
            // guild so a coalition guild keeps posting/pinging as before; a guild with no link
            // keeps nothing (there'd be nowhere to scope it, and no alliance features run either).
            // Feature ids: TerritoryCapture = 2, Announcements = 3 (GuildFeature enum order).
            migrationBuilder.Sql("""
                INSERT INTO "GuildFeatureSettingSnowflakes" ("GuildId", "Feature", "Audience", "GuildAllianceId", "Key", "Value")
                SELECT gs."GuildId", 2, 1, ga."Id", 'DigestChannel', gs."RemindersChannelId"
                FROM "GuildSettings" gs
                JOIN "GuildAlliances" ga ON ga."GuildId" = gs."GuildId"
                WHERE gs."RemindersChannelId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "GuildFeatureSettingSnowflakes" s
                     WHERE s."GuildId" = gs."GuildId" AND s."Feature" = 2 AND s."Audience" = 1
                       AND s."GuildAllianceId" = ga."Id" AND s."Key" = 'DigestChannel');
                """);
            migrationBuilder.Sql("""
                INSERT INTO "GuildFeatureSettingSnowflakes" ("GuildId", "Feature", "Audience", "GuildAllianceId", "Key", "Value")
                SELECT gs."GuildId", 3, 1, ga."Id", 'WarningsRole', gs."WarningsRoleId"
                FROM "GuildSettings" gs
                JOIN "GuildAlliances" ga ON ga."GuildId" = gs."GuildId"
                WHERE gs."WarningsRoleId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "GuildFeatureSettingSnowflakes" s
                     WHERE s."GuildId" = gs."GuildId" AND s."Feature" = 3 AND s."Audience" = 1
                       AND s."GuildAllianceId" = ga."Id" AND s."Key" = 'WarningsRole');
                """);

            migrationBuilder.DropColumn(
                name: "RemindersChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "WarningsRoleId",
                table: "GuildSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RemindersChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WarningsRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);
        }
    }
}
