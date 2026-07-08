using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAllianceExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StfcAlliances_ServerId_Tag",
                table: "StfcAlliances");

            migrationBuilder.AddColumn<long>(
                name: "ExternalId",
                table: "StfcAlliances",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_StfcAlliances_ExternalId",
                table: "StfcAlliances",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcAlliances_ServerId",
                table: "StfcAlliances",
                column: "ServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StfcAlliances_ExternalId",
                table: "StfcAlliances");

            migrationBuilder.DropIndex(
                name: "IX_StfcAlliances_ServerId",
                table: "StfcAlliances");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "StfcAlliances");

            migrationBuilder.CreateIndex(
                name: "IX_StfcAlliances_ServerId_Tag",
                table: "StfcAlliances",
                columns: new[] { "ServerId", "Tag" },
                unique: true);
        }
    }
}
