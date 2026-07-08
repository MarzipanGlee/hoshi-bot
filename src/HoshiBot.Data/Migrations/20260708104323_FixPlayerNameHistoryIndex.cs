using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPlayerNameHistoryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StfcPlayerNameHistories_StfcPlayerId_Name",
                table: "StfcPlayerNameHistories");

            migrationBuilder.CreateIndex(
                name: "IX_StfcPlayerNameHistories_StfcPlayerId",
                table: "StfcPlayerNameHistories",
                column: "StfcPlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StfcPlayerNameHistories_StfcPlayerId",
                table: "StfcPlayerNameHistories");

            migrationBuilder.CreateIndex(
                name: "IX_StfcPlayerNameHistories_StfcPlayerId_Name",
                table: "StfcPlayerNameHistories",
                columns: new[] { "StfcPlayerId", "Name" },
                unique: true);
        }
    }
}
