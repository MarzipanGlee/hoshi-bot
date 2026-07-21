using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddForwardedAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ForwardedAnnouncements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SourceChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SourceMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DestinationChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DestinationMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SourceContentHash = table.Column<string>(type: "text", nullable: false),
                    ForwardedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForwardedAnnouncements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForwardedAnnouncements_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForwardedAnnouncements_GuildId",
                table: "ForwardedAnnouncements",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardedAnnouncements_SourceMessageId",
                table: "ForwardedAnnouncements",
                column: "SourceMessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForwardedAnnouncements");
        }
    }
}
