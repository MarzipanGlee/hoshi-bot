using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTerritoryServiceSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TerritoryServiceSelections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildAllianceId = table.Column<int>(type: "integer", nullable: false),
                    TerritoryId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritoryServiceSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerritoryServiceSelections_GuildAlliances_GuildAllianceId",
                        column: x => x.GuildAllianceId,
                        principalTable: "GuildAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TerritoryServiceSelections_StfcTerritories_TerritoryId",
                        column: x => x.TerritoryId,
                        principalTable: "StfcTerritories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerritoryServiceSelections_StfcTerritoryServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "StfcTerritoryServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryServiceSelections_GuildAllianceId_TerritoryId",
                table: "TerritoryServiceSelections",
                columns: new[] { "GuildAllianceId", "TerritoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryServiceSelections_GuildAllianceId_TerritoryId_Serv~",
                table: "TerritoryServiceSelections",
                columns: new[] { "GuildAllianceId", "TerritoryId", "ServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryServiceSelections_ServiceId",
                table: "TerritoryServiceSelections",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryServiceSelections_TerritoryId",
                table: "TerritoryServiceSelections",
                column: "TerritoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TerritoryServiceSelections");
        }
    }
}
