using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class DedupeAndUniqueTerritoryOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StfcTerritoryOwnerships_TerritoryId_ServerId",
                table: "StfcTerritoryOwnerships");

            // Collapse duplicate ownership rows (concurrent Host/Web seeding doubled every row)
            // before the unique index goes on, keeping the lowest Id per (TerritoryId, ServerId) —
            // otherwise the CreateIndex below fails on the existing duplicates.
            migrationBuilder.Sql("""
                DELETE FROM "StfcTerritoryOwnerships" a
                USING "StfcTerritoryOwnerships" b
                WHERE a."TerritoryId" = b."TerritoryId"
                  AND a."ServerId" = b."ServerId"
                  AND a."Id" > b."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryOwnerships_TerritoryId_ServerId",
                table: "StfcTerritoryOwnerships",
                columns: new[] { "TerritoryId", "ServerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StfcTerritoryOwnerships_TerritoryId_ServerId",
                table: "StfcTerritoryOwnerships");

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryOwnerships_TerritoryId_ServerId",
                table: "StfcTerritoryOwnerships",
                columns: new[] { "TerritoryId", "ServerId" });
        }
    }
}
