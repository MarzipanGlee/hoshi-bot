using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// The Diplomat role moves from the Diplomacy feature's settings to the alliance that owns it,
    /// where both features reading it (Diplomacy, RoE Violation Reports) can find it.
    ///
    /// No schema change — GuildAlliances.DiplomatRoleId already existed and was read by nothing. This
    /// is purely the data move, and the direction matters: the FEATURE SETTING is authoritative
    /// because it is the value the bot actually read. On the test data the two disagreed for one
    /// alliance (column …122654 vs setting …447381), so copying the wrong way would have silently
    /// changed who gets pinged on a RoE case.
    ///
    /// Alliances whose column already held a value but had no setting row keep it. Those values were
    /// inert before (nothing read the column) and become live here, which is the point of the move —
    /// an admin who filled that picker in meant it.
    /// </summary>
    public partial class DiplomatRolePerAlliance : Migration
    {
        // GuildFeature.Diplomacy. Pinned as a literal: the enum is append-only, so this ordinal is
        // stable, and a migration must not shift meaning if the enum is ever reordered by mistake.
        private const int DiplomacyFeature = 9;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE "GuildAlliances" a
                SET "DiplomatRoleId" = s."Value"
                FROM "GuildFeatureSettingSnowflakes" s
                WHERE s."Feature" = {DiplomacyFeature}
                  AND s."Key" = 'DiplomatRole'
                  AND s."GuildAllianceId" = a."Id"
                  AND s."GuildId" = a."GuildId";
                """);

            migrationBuilder.Sql($"""
                DELETE FROM "GuildFeatureSettingSnowflakes"
                WHERE "Feature" = {DiplomacyFeature} AND "Key" = 'DiplomatRole';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best effort: every alliance holding a role becomes a setting row again. It cannot tell
            // which of those came from a setting row originally, so going down and up again would
            // leave the column-only alliances with settings they never had. Harmless — the column
            // keeps its value either way.
            migrationBuilder.Sql($"""
                INSERT INTO "GuildFeatureSettingSnowflakes"
                    ("GuildId", "Feature", "Audience", "Key", "Value", "GuildAllianceId")
                SELECT a."GuildId", {DiplomacyFeature}, 1, 'DiplomatRole', a."DiplomatRoleId", a."Id"
                FROM "GuildAlliances" a
                WHERE a."DiplomatRoleId" IS NOT NULL;
                """);
        }
    }
}
