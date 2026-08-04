using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConditionalRolePlayerOperand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StfcPlayerId",
                table: "ConditionalRoleNodes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConditionalRoleNodes_StfcPlayerId",
                table: "ConditionalRoleNodes",
                column: "StfcPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConditionalRoleNodes_StfcPlayers_StfcPlayerId",
                table: "ConditionalRoleNodes",
                column: "StfcPlayerId",
                principalTable: "StfcPlayers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConditionalRoleNodes_StfcPlayers_StfcPlayerId",
                table: "ConditionalRoleNodes");

            migrationBuilder.DropIndex(
                name: "IX_ConditionalRoleNodes_StfcPlayerId",
                table: "ConditionalRoleNodes");

            migrationBuilder.DropColumn(
                name: "StfcPlayerId",
                table: "ConditionalRoleNodes");
        }
    }
}
