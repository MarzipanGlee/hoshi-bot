using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandBridgesAndRepublishQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FriendsCommandBridgeChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FriendsCommandBridgeMessageId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StaffCommandBridgeChannelId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StaffCommandBridgeMessageId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommandBridgeRepublishRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Bridge = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandBridgeRepublishRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandBridgeRepublishRequests_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandBridgeRepublishRequests_GuildId",
                table: "CommandBridgeRepublishRequests",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandBridgeRepublishRequests_RequestedAt",
                table: "CommandBridgeRepublishRequests",
                column: "RequestedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandBridgeRepublishRequests");

            migrationBuilder.DropColumn(
                name: "FriendsCommandBridgeChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "FriendsCommandBridgeMessageId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "StaffCommandBridgeChannelId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "StaffCommandBridgeMessageId",
                table: "GuildSettings");
        }
    }
}
