using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// Member Lore stops being alliance-only, so an interview has to record which scope invited it.
    ///
    /// Existing rows are backfilled with Alliance (1), not the enum's default None (0): every
    /// interview that exists was created while Alliance was the feature's only audience. Left at
    /// None they would look for their completed-role setting in a scope that has none, and an
    /// in-flight interview would finish without granting the role — silently.
    /// </summary>
    public partial class MemberLoreForEveryAudience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Audience",
                table: "MemberInterviews",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Audience",
                table: "MemberInterviews");
        }
    }
}
