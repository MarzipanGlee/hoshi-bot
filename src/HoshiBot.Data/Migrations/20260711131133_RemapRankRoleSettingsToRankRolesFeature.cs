using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemapRankRoleSettingsToRankRolesFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GuildFeature.RankRoles (12) is a brand-new feature; its 5 role settings
            // previously lived under TerritoryCapture (2)/Alliance (1) as a borrowed home
            // (see SettingsEditor.razor's old "Rank Roles" section) — remap in place so
            // already-configured roles aren't lost. TerritoryCapture's real settings
            // (ZoneSlot1Role..5Role, Instructions) use different Key strings and are untouched.
            migrationBuilder.Sql(
                """
                UPDATE "GuildFeatureSettingSnowflakes"
                SET "Feature" = 12
                WHERE "Feature" = 2
                  AND "Audience" = 1
                  AND "Key" IN ('AdmiralRole', 'CommodoreRole', 'PremierRole', 'OperativeRole', 'AgentRole');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "GuildFeatureSettingSnowflakes"
                SET "Feature" = 2
                WHERE "Feature" = 12
                  AND "Audience" = 1
                  AND "Key" IN ('AdmiralRole', 'CommodoreRole', 'PremierRole', 'OperativeRole', 'AgentRole');
                """);
        }
    }
}
