using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTerritoryServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StfcTerritoryServices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    LocaId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    InfoShort = table.Column<string>(type: "text", nullable: true),
                    Rarity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcTerritoryServices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TerritoryServiceSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TcSeason = table.Column<string>(type: "text", nullable: true),
                    GeneratedAt = table.Column<long>(type: "bigint", nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritoryServiceSyncStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StfcTerritoryServiceSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    TerritoryId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcTerritoryServiceSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcTerritoryServiceSlots_StfcServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "StfcServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StfcTerritoryServiceSlots_StfcTerritories_TerritoryId",
                        column: x => x.TerritoryId,
                        principalTable: "StfcTerritories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StfcTerritoryServiceSlots_StfcTerritoryServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "StfcTerritoryServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryServiceSlots_ServerId_TerritoryId",
                table: "StfcTerritoryServiceSlots",
                columns: new[] { "ServerId", "TerritoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryServiceSlots_ServerId_TerritoryId_ServiceId",
                table: "StfcTerritoryServiceSlots",
                columns: new[] { "ServerId", "TerritoryId", "ServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryServiceSlots_ServiceId",
                table: "StfcTerritoryServiceSlots",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcTerritoryServiceSlots_TerritoryId",
                table: "StfcTerritoryServiceSlots",
                column: "TerritoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StfcTerritoryServiceSlots");

            migrationBuilder.DropTable(
                name: "TerritoryServiceSyncStates");

            migrationBuilder.DropTable(
                name: "StfcTerritoryServices");
        }
    }
}
