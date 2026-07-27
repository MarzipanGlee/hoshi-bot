using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Locale",
                table: "DiscordGuilds");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "GuildSettings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "GuildAlliances",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordLocale",
                table: "DiscordUsers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "DiscordUsers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLocale",
                table: "DiscordGuilds",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GuildAudienceLanguages",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildAudienceLanguages", x => new { x.GuildId, x.Audience });
                    table.ForeignKey(
                        name: "FK_GuildAudienceLanguages_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildAudienceLanguages");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "GuildAlliances");

            migrationBuilder.DropColumn(
                name: "DiscordLocale",
                table: "DiscordUsers");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "DiscordUsers");

            migrationBuilder.DropColumn(
                name: "PreferredLocale",
                table: "DiscordGuilds");

            migrationBuilder.AddColumn<string>(
                name: "Locale",
                table: "DiscordGuilds",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }
    }
}
