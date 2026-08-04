using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConditionalRoleAllianceOperand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StfcAllianceId",
                table: "ConditionalRoleNodes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConditionalRoleNodes_StfcAllianceId",
                table: "ConditionalRoleNodes",
                column: "StfcAllianceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConditionalRoleNodes_StfcAlliances_StfcAllianceId",
                table: "ConditionalRoleNodes",
                column: "StfcAllianceId",
                principalTable: "StfcAlliances",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConditionalRoleNodes_StfcAlliances_StfcAllianceId",
                table: "ConditionalRoleNodes");

            migrationBuilder.DropIndex(
                name: "IX_ConditionalRoleNodes_StfcAllianceId",
                table: "ConditionalRoleNodes");

            migrationBuilder.DropColumn(
                name: "StfcAllianceId",
                table: "ConditionalRoleNodes");
        }
    }
}
