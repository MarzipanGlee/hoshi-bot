using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExtractedAt",
                table: "MemberInterviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GuildMemberNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PreferredName = table.Column<string>(type: "text", nullable: true),
                    Nicknames = table.Column<string>(type: "text", nullable: true),
                    Interests = table.Column<string>(type: "text", nullable: true),
                    Background = table.Column<string>(type: "text", nullable: true),
                    Languages = table.Column<string>(type: "text", nullable: true),
                    RunningJokes = table.Column<string>(type: "text", nullable: true),
                    TeaseAbout = table.Column<string>(type: "text", nullable: true),
                    PeerLoreHidden = table.Column<bool>(type: "boolean", nullable: false),
                    SelfUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PeerUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildMemberNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildMemberNotes_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberNoteSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TargetDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    TargetNameRaw = table.Column<string>(type: "text", nullable: false),
                    Field = table.Column<int>(type: "integer", nullable: false),
                    SuggestedText = table.Column<string>(type: "text", nullable: false),
                    SourceInterviewId = table.Column<int>(type: "integer", nullable: true),
                    SourceDiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberNoteSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberNoteSuggestions_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberNoteSuggestions_MemberInterviews_SourceInterviewId",
                        column: x => x.SourceInterviewId,
                        principalTable: "MemberInterviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildMemberNotes_GuildId_DiscordUserId",
                table: "GuildMemberNotes",
                columns: new[] { "GuildId", "DiscordUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberNoteSuggestions_GuildId_Status",
                table: "MemberNoteSuggestions",
                columns: new[] { "GuildId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberNoteSuggestions_SourceInterviewId",
                table: "MemberNoteSuggestions",
                column: "SourceInterviewId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildMemberNotes");

            migrationBuilder.DropTable(
                name: "MemberNoteSuggestions");

            migrationBuilder.DropColumn(
                name: "ExtractedAt",
                table: "MemberInterviews");
        }
    }
}
