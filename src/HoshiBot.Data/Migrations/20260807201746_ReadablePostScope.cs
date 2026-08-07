using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// Records which scope each readable post was made for, now that Read Confirmation is
    /// per-audience. Existing rows default to the alliance scope their guild was migrated to, which
    /// is where they were actually published — they were all announcements, and every guild on this
    /// deployment has exactly one alliance.
    ///
    /// Their ReadReceiptsEnabled flag is untouched: what a member is already being asked to confirm
    /// does not change because the setting moved scope. That is the flag's whole job.
    /// </summary>
    public partial class ReadablePostScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Audience",
                table: "ReadablePosts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GuildAllianceId",
                table: "ReadablePosts",
                type: "integer",
                nullable: true);

            // Existing posts predate the column. They were all announcements published into the
            // guild's single alliance, which is the scope the previous migration moved that guild's
            // settings to — so point them there rather than leaving them at audience 0 ("None"),
            // which is not a scope any setting is ever read from.
            migrationBuilder.Sql("""
                UPDATE "ReadablePosts" p
                SET "Audience" = 1, "GuildAllianceId" = g.alliance_id
                FROM (
                    SELECT ga."GuildId", min(ga."Id") AS alliance_id
                    FROM "GuildAlliances" ga
                    GROUP BY ga."GuildId"
                    HAVING count(*) = 1
                ) g
                WHERE p."GuildId" = g."GuildId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Audience",
                table: "ReadablePosts");

            migrationBuilder.DropColumn(
                name: "GuildAllianceId",
                table: "ReadablePosts");
        }
    }
}
