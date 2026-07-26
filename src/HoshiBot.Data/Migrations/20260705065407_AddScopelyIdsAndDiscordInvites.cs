using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScopelyIdsAndDiscordInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StfcServers_Number",
                table: "StfcServers");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "StfcServers");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "StfcVeilGroups",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "StfcServers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "StfcRegions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateTable(
                name: "StfcAllianceDiscordInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AllianceId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcAllianceDiscordInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcAllianceDiscordInvites_StfcAlliances_AllianceId",
                        column: x => x.AllianceId,
                        principalTable: "StfcAlliances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcServerDiscordInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcServerDiscordInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcServerDiscordInvites_StfcServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "StfcServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StfcVeilGroupDiscordInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VeilGroupId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StfcVeilGroupDiscordInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StfcVeilGroupDiscordInvites_StfcVeilGroups_VeilGroupId",
                        column: x => x.VeilGroupId,
                        principalTable: "StfcVeilGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StfcAllianceDiscordInvites_AllianceId",
                table: "StfcAllianceDiscordInvites",
                column: "AllianceId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcServerDiscordInvites_ServerId",
                table: "StfcServerDiscordInvites",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_StfcVeilGroupDiscordInvites_VeilGroupId",
                table: "StfcVeilGroupDiscordInvites",
                column: "VeilGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StfcAllianceDiscordInvites");

            migrationBuilder.DropTable(
                name: "StfcServerDiscordInvites");

            migrationBuilder.DropTable(
                name: "StfcVeilGroupDiscordInvites");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "StfcVeilGroups",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "StfcServers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "StfcServers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "StfcRegions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "IX_StfcServers_Number",
                table: "StfcServers",
                column: "Number",
                unique: true);
        }
    }
}
