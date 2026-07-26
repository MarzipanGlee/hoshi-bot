using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildMemberPrimaryPlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-edited: EF scaffolded the DropColumn first, which would have thrown away every
            // member's main player before there was anywhere to put it. Order is add → backfill →
            // drop, so each guild's pick starts life as whatever the global main used to be.
            migrationBuilder.AddColumn<int>(
                name: "PrimaryStfcPlayerId",
                table: "GuildMembers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_PrimaryStfcPlayerId",
                table: "GuildMembers",
                column: "PrimaryStfcPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildMembers_StfcPlayers_PrimaryStfcPlayerId",
                table: "GuildMembers",
                column: "PrimaryStfcPlayerId",
                principalTable: "StfcPlayers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql("""
                UPDATE "GuildMembers" gm
                SET "PrimaryStfcPlayerId" = up."StfcPlayerId"
                FROM "UserPlayers" up
                WHERE up."DiscordUserId" = gm."DiscordUserId" AND up."IsMain";
                """);

            migrationBuilder.DropColumn(
                name: "IsMain",
                table: "UserPlayers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMain",
                table: "UserPlayers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Best-effort restore: one main per user — the player any of their guilds points at,
            // else their oldest link. A per-guild choice can't survive a global flag, so a member
            // who picked different players in different guilds keeps only one of them.
            migrationBuilder.Sql("""
                UPDATE "UserPlayers" up
                SET "IsMain" = true
                WHERE up."Id" = (
                    SELECT candidate."Id"
                    FROM "UserPlayers" candidate
                    LEFT JOIN "GuildMembers" gm
                        ON gm."DiscordUserId" = candidate."DiscordUserId"
                       AND gm."PrimaryStfcPlayerId" = candidate."StfcPlayerId"
                    WHERE candidate."DiscordUserId" = up."DiscordUserId"
                    ORDER BY (gm."PrimaryStfcPlayerId" IS NOT NULL) DESC, candidate."Id"
                    LIMIT 1);
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_GuildMembers_StfcPlayers_PrimaryStfcPlayerId",
                table: "GuildMembers");

            migrationBuilder.DropIndex(
                name: "IX_GuildMembers_PrimaryStfcPlayerId",
                table: "GuildMembers");

            migrationBuilder.DropColumn(
                name: "PrimaryStfcPlayerId",
                table: "GuildMembers");
        }
    }
}
