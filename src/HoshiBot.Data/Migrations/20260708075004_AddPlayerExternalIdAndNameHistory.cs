using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerExternalIdAndNameHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StfcPlayers_ServerId_Name",
                table: "StfcPlayers");

            migrationBuilder.AddColumn<long>(
                name: "ExternalId",
                table: "StfcPlayers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "StfcPlayerNameHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StfcPlayerId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcPlayerNameHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcPlayerNameHistories_StfcPlayers_StfcPlayerId",
                        column: x => x.StfcPlayerId,
                        principalTable: "StfcPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StfcPlayers_ExternalId",
                table: "StfcPlayers",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StfcPlayers_ServerId",
                table: "StfcPlayers",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcPlayerNameHistories_StfcPlayerId_Name",
                table: "StfcPlayerNameHistories",
                columns: new[] { "StfcPlayerId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StfcPlayerNameHistories");

            migrationBuilder.DropIndex(
                name: "IX_StfcPlayers_ExternalId",
                table: "StfcPlayers");

            migrationBuilder.DropIndex(
                name: "IX_StfcPlayers_ServerId",
                table: "StfcPlayers");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "StfcPlayers");

            migrationBuilder.CreateIndex(
                name: "IX_StfcPlayers_ServerId_Name",
                table: "StfcPlayers",
                columns: new[] { "ServerId", "Name" },
                unique: true);
        }
    }
}
