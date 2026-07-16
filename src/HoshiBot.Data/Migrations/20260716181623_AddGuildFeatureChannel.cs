using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildFeatureChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuildFeatureChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Feature = table.Column<int>(type: "integer", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildFeatureChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildFeatureChannels_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureChannels_GuildId_Feature_Audience_ChannelId",
                table: "GuildFeatureChannels",
                columns: new[] { "GuildId", "Feature", "Audience", "ChannelId" },
                unique: true);

            // ClientRelease moves off GuildAlertChannel (channel+role pairs) onto this
            // channel-only list — its role is now decided per platform, not per channel. Carry
            // any existing ClientRelease alert channels over (Kind 5 = GuildAlertChannelKind.
            // ClientRelease, Feature 16 = GuildFeature.ClientRelease; Audience column is shared),
            // then drop the now-orphaned rows so the old kind has no data behind it.
            migrationBuilder.Sql("""
                INSERT INTO "GuildFeatureChannels" ("GuildId", "Feature", "Audience", "ChannelId")
                SELECT "GuildId", 16, "Audience", "ChannelId" FROM "GuildAlertChannels" WHERE "Kind" = 5;
                """);
            migrationBuilder.Sql("""DELETE FROM "GuildAlertChannels" WHERE "Kind" = 5;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildFeatureChannels");
        }
    }
}
