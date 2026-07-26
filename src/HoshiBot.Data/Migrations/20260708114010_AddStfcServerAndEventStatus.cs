using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStfcServerAndEventStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StfcEventStatuses",
                columns: table => new
                {
                    EventGroup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EventStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotifiedEventStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcEventStatuses", x => x.EventGroup);
                });

            migrationBuilder.CreateTable(
                name: "StfcServerStatuses",
                columns: table => new
                {
                    StfcServerId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Maintenance = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotifiedStatus = table.Column<int>(type: "integer", nullable: true),
                    NotifiedMaintenance = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcServerStatuses", x => x.StfcServerId);
                    table.ForeignKey(
                        name: "FK_StfcServerStatuses_StfcServers_StfcServerId",
                        column: x => x.StfcServerId,
                        principalTable: "StfcServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StfcEventStatuses");

            migrationBuilder.DropTable(
                name: "StfcServerStatuses");
        }
    }
}
