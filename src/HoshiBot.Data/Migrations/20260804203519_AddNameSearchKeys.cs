using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNameSearchKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameKey",
                table: "StfcPlayers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameKey",
                table: "StfcAlliances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagKey",
                table: "StfcAlliances",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcPlayers_NameKey",
                table: "StfcPlayers",
                column: "NameKey");

            migrationBuilder.CreateIndex(
                name: "IX_StfcAlliances_NameKey",
                table: "StfcAlliances",
                column: "NameKey");

            migrationBuilder.CreateIndex(
                name: "IX_StfcAlliances_TagKey",
                table: "StfcAlliances",
                column: "TagKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StfcPlayers_NameKey",
                table: "StfcPlayers");

            migrationBuilder.DropIndex(
                name: "IX_StfcAlliances_NameKey",
                table: "StfcAlliances");

            migrationBuilder.DropIndex(
                name: "IX_StfcAlliances_TagKey",
                table: "StfcAlliances");

            migrationBuilder.DropColumn(
                name: "NameKey",
                table: "StfcPlayers");

            migrationBuilder.DropColumn(
                name: "NameKey",
                table: "StfcAlliances");

            migrationBuilder.DropColumn(
                name: "TagKey",
                table: "StfcAlliances");
        }
    }
}
