using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnableCommandBridgeForConfiguredAlliances : Migration
    {
        // Command Bridge became a GuildFeature (enum ordinal 25) that gates hub posting. Existing
        // alliances that already have a bridge channel configured must be enabled so their hubs keep
        // posting and the new "Requires: Command Bridge" dependency on the eight button-features
        // resolves as satisfied. Data-only (no schema change). 25 = GuildFeature.CommandBridge,
        // 1 = GuildAudience.Alliance.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "GuildEnabledFeatures" ("GuildId", "Feature", "Audience", "GuildAllianceId")
                SELECT ga."GuildId", 25, 1, ga."Id"
                FROM "GuildAlliances" ga
                WHERE (ga."CommandBridgeChannelId" IS NOT NULL
                    OR ga."StaffCommandBridgeChannelId" IS NOT NULL
                    OR ga."FriendsCommandBridgeChannelId" IS NOT NULL)
                  AND NOT EXISTS (
                    SELECT 1 FROM "GuildEnabledFeatures" e
                    WHERE e."GuildId" = ga."GuildId" AND e."Feature" = 25 AND e."Audience" = 1 AND e."GuildAllianceId" = ga."Id");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""GuildEnabledFeatures"" WHERE ""Feature"" = 25;");
        }
    }
}
