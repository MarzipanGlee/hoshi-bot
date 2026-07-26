using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildAudienceFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Audiences",
                table: "GuildSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // One-time best-effort backfill so existing guilds don't regress to "no
            // audience set" after this deploy — not live derivation logic, the column is
            // the source of truth going forward (editable via the Setup Wizard's Audience
            // step or Global Settings). Bitwise-OR, not a plain SET, so a guild matching
            // both conditions ends up with both flags (1 = Alliance, 2 = ServerVeilGroup).
            migrationBuilder.Sql(
                """
                UPDATE "GuildSettings" SET "Audiences" = "Audiences" | 1
                WHERE "GuildId" IN (SELECT DISTINCT "GuildId" FROM "GuildAlliances");
                """);
            migrationBuilder.Sql(
                """
                UPDATE "GuildSettings" SET "Audiences" = "Audiences" | 2
                WHERE "GuildId" IN (
                    SELECT DISTINCT "GuildId" FROM "GuildServers"
                    UNION
                    SELECT DISTINCT "GuildId" FROM "GuildVeilGroups"
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Audiences",
                table: "GuildSettings");
        }
    }
}
