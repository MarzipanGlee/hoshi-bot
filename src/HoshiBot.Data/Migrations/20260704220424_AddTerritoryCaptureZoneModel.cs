using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTerritoryCaptureZoneModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StfcTerritories_StfcSystems_SystemId",
                table: "StfcTerritories");

            migrationBuilder.DropIndex(
                name: "IX_StfcTerritories_SystemId",
                table: "StfcTerritories");

            migrationBuilder.RenameColumn(
                name: "SystemId",
                table: "StfcTerritories",
                newName: "Tier");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CaptureTimeUtc",
                table: "StfcTerritories",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "StfcTerritories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Weekday",
                table: "StfcTerritories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TerritoryId",
                table: "StfcSystems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerritoryCaptureInstructions",
                table: "GuildSettings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StfcTerritoryNeighbours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TerritoryId = table.Column<int>(type: "integer", nullable: false),
                    NeighbourTerritoryId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcTerritoryNeighbours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcTerritoryNeighbours_StfcTerritories_NeighbourTerritoryId",
                        column: x => x.NeighbourTerritoryId,
                        principalTable: "StfcTerritories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StfcTerritoryNeighbours_StfcTerritories_TerritoryId",
                        column: x => x.TerritoryId,
                        principalTable: "StfcTerritories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritories_Name",
                table: "StfcTerritories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcSystems_TerritoryId",
                table: "StfcSystems",
                column: "TerritoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryNeighbours_NeighbourTerritoryId",
                table: "StfcTerritoryNeighbours",
                column: "NeighbourTerritoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryNeighbours_TerritoryId_NeighbourTerritoryId",
                table: "StfcTerritoryNeighbours",
                columns: new[] { "TerritoryId", "NeighbourTerritoryId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StfcSystems_StfcTerritories_TerritoryId",
                table: "StfcSystems",
                column: "TerritoryId",
                principalTable: "StfcTerritories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StfcSystems_StfcTerritories_TerritoryId",
                table: "StfcSystems");

            migrationBuilder.DropTable(
                name: "StfcTerritoryNeighbours");

            migrationBuilder.DropIndex(
                name: "IX_StfcTerritories_Name",
                table: "StfcTerritories");

            migrationBuilder.DropIndex(
                name: "IX_StfcSystems_TerritoryId",
                table: "StfcSystems");

            migrationBuilder.DropColumn(
                name: "CaptureTimeUtc",
                table: "StfcTerritories");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "StfcTerritories");

            migrationBuilder.DropColumn(
                name: "Weekday",
                table: "StfcTerritories");

            migrationBuilder.DropColumn(
                name: "TerritoryId",
                table: "StfcSystems");

            migrationBuilder.DropColumn(
                name: "TerritoryCaptureInstructions",
                table: "GuildSettings");

            migrationBuilder.RenameColumn(
                name: "Tier",
                table: "StfcTerritories",
                newName: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritories_SystemId",
                table: "StfcTerritories",
                column: "SystemId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StfcTerritories_StfcSystems_SystemId",
                table: "StfcTerritories",
                column: "SystemId",
                principalTable: "StfcSystems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
