using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// The alliance notification role moves out of the Absences feature settings and onto
    /// GuildAlliance, beside the member/officer/diplomat/boarding roles it belongs with.
    ///
    /// It was never really an Absences setting: three features ping it, and all three editors could
    /// change it — which read as three settings that mysteriously moved together, and gave each page
    /// its own chance to name the role it might create. One owner (the alliance settings page),
    /// three readers.
    ///
    /// Feature = Absences = 7, GuildAudience.Alliance = 1, per GuildFeature's "keep last" comment.
    /// </summary>
    public partial class NotificationRoleMovesToAlliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NotificationRoleId",
                table: "GuildAlliances",
                type: "numeric(20,0)",
                nullable: true);

            // Carry every configured role across before the rows go.
            migrationBuilder.Sql("""
                UPDATE "GuildAlliances" a
                SET "NotificationRoleId" = s."Value"
                FROM "GuildFeatureSettingSnowflakes" s
                WHERE s."Feature" = 7
                  AND s."Audience" = 1
                  AND s."Key" = 'NotificationRole'
                  AND s."GuildAllianceId" = a."Id";
                """);

            migrationBuilder.Sql("""
                DELETE FROM "GuildFeatureSettingSnowflakes" WHERE "Feature" = 7 AND "Key" = 'NotificationRole';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationRoleId",
                table: "GuildAlliances");

            migrationBuilder.Sql("""
                INSERT INTO "GuildFeatureSettingSnowflakes" ("GuildId", "Feature", "Audience", "GuildAllianceId", "Key", "Value")
                SELECT a."GuildId", 7, 1, a."Id", 'NotificationRole', a."NotificationRoleId"
                FROM "GuildAlliances" a
                WHERE a."NotificationRoleId" IS NOT NULL
                ON CONFLICT DO NOTHING;
                """);
        }
    }
}
