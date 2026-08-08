using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AudienceSettingsHoldTheChannelCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A RENAME plus a column, not the DropTable/CreateTable EF scaffolded for it — that
            // would have discarded every audience language a guild had set. (README: "EF's rename
            // detection is unreliable"; this is exactly the case it warns about.)
            migrationBuilder.RenameTable(
                name: "GuildAudienceLanguages",
                newName: "GuildAudienceSettings");

            migrationBuilder.Sql("""
                ALTER TABLE "GuildAudienceSettings" RENAME CONSTRAINT "PK_GuildAudienceLanguages" TO "PK_GuildAudienceSettings";
                ALTER TABLE "GuildAudienceSettings" RENAME CONSTRAINT "FK_GuildAudienceLanguages_DiscordGuilds_GuildId" TO "FK_GuildAudienceSettings_DiscordGuilds_GuildId";
                """);

            // Language stops being required: a row now exists for either setting, so one may be null
            // while the other is set.
            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "GuildAudienceSettings",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultChannelCategoryId",
                table: "GuildAudienceSettings",
                type: "numeric(20,0)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A row that only ever carried a category has no language to go back to, and the column
            // is about to be NOT NULL again — drop those rather than fail the migration.
            migrationBuilder.Sql("""
                DELETE FROM "GuildAudienceSettings" WHERE "Language" IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "DefaultChannelCategoryId",
                table: "GuildAudienceSettings");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "GuildAudienceSettings",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.Sql("""
                ALTER TABLE "GuildAudienceSettings" RENAME CONSTRAINT "PK_GuildAudienceSettings" TO "PK_GuildAudienceLanguages";
                ALTER TABLE "GuildAudienceSettings" RENAME CONSTRAINT "FK_GuildAudienceSettings_DiscordGuilds_GuildId" TO "FK_GuildAudienceLanguages_DiscordGuilds_GuildId";
                """);

            migrationBuilder.RenameTable(
                name: "GuildAudienceSettings",
                newName: "GuildAudienceLanguages");
        }
    }
}
