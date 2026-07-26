using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStfcSystemFkToAlertAndShieldReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "System",
                table: "ShieldReminders");

            migrationBuilder.DropColumn(
                name: "System",
                table: "Alerts");

            migrationBuilder.AddColumn<int>(
                name: "StfcSystemId",
                table: "ShieldReminders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StfcSystemId",
                table: "Alerts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShieldReminders_StfcSystemId",
                table: "ShieldReminders",
                column: "StfcSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_StfcSystemId",
                table: "Alerts",
                column: "StfcSystemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_StfcSystems_StfcSystemId",
                table: "Alerts",
                column: "StfcSystemId",
                principalTable: "StfcSystems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ShieldReminders_StfcSystems_StfcSystemId",
                table: "ShieldReminders",
                column: "StfcSystemId",
                principalTable: "StfcSystems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_StfcSystems_StfcSystemId",
                table: "Alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_ShieldReminders_StfcSystems_StfcSystemId",
                table: "ShieldReminders");

            migrationBuilder.DropIndex(
                name: "IX_ShieldReminders_StfcSystemId",
                table: "ShieldReminders");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_StfcSystemId",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "StfcSystemId",
                table: "ShieldReminders");

            migrationBuilder.DropColumn(
                name: "StfcSystemId",
                table: "Alerts");

            migrationBuilder.AddColumn<string>(
                name: "System",
                table: "ShieldReminders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "System",
                table: "Alerts",
                type: "text",
                nullable: true);
        }
    }
}
