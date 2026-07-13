using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropTerritoryOwnershipLastCapturedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastCapturedAt",
                table: "StfcTerritoryOwnerships");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastCapturedAt",
                table: "StfcTerritoryOwnerships",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
