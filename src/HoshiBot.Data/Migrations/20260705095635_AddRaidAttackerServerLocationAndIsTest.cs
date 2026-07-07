using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidAttackerServerLocationAndIsTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Attacker",
                table: "Alerts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTest",
                table: "Alerts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ServerLocation",
                table: "Alerts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attacker",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "IsTest",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "ServerLocation",
                table: "Alerts");
        }
    }
}
