using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    // Hand-edited: the scaffolded migration would have (1) DropTable'd
    // GuildTerritoryCaptureZoneSlotRoles without copying its 5 rows into the new fixed
    // ZoneSlot1RoleId..ZoneSlot5RoleId columns, and (2) RenameColumn'd CommodoresRoleId
    // straight into ZoneSlot5RoleId (EF's diff just found a same-typed column to reuse,
    // with no regard for what it actually means) — both would have silently corrupted or
    // discarded real data. This version adds the new columns first, copies data into them,
    // then drops the old table/column.
    public partial class CollapseZoneSlotRolesAndAddRankRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdmiralRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AgentRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommodoreRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OperativeRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PremierRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZoneSlot1RoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZoneSlot2RoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZoneSlot3RoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZoneSlot4RoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ZoneSlot5RoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"GuildSettings\" SET \"CommodoreRoleId\" = \"CommodoresRoleId\";");

            for (var slot = 1; slot <= 5; slot++)
            {
                migrationBuilder.Sql($"""
                    UPDATE "GuildSettings" gs
                    SET "ZoneSlot{slot}RoleId" = t."RoleId"
                    FROM "GuildTerritoryCaptureZoneSlotRoles" t
                    WHERE t."GuildId" = gs."GuildId" AND t."SlotIndex" = {slot};
                    """);
            }

            migrationBuilder.DropColumn(
                name: "CommodoresRoleId",
                table: "GuildSettings");

            migrationBuilder.DropTable(
                name: "GuildTerritoryCaptureZoneSlotRoles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommodoresRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"GuildSettings\" SET \"CommodoresRoleId\" = \"CommodoreRoleId\";");

            migrationBuilder.CreateTable(
                name: "GuildTerritoryCaptureZoneSlotRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SlotIndex = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildTerritoryCaptureZoneSlotRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildTerritoryCaptureZoneSlotRoles_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildTerritoryCaptureZoneSlotRoles_GuildId_SlotIndex",
                table: "GuildTerritoryCaptureZoneSlotRoles",
                columns: new[] { "GuildId", "SlotIndex" },
                unique: true);

            for (var slot = 1; slot <= 5; slot++)
            {
                migrationBuilder.Sql($"""
                    INSERT INTO "GuildTerritoryCaptureZoneSlotRoles" ("GuildId", "SlotIndex", "RoleId")
                    SELECT "GuildId", {slot}, "ZoneSlot{slot}RoleId"
                    FROM "GuildSettings"
                    WHERE "ZoneSlot{slot}RoleId" IS NOT NULL;
                    """);
            }

            migrationBuilder.DropColumn(
                name: "AdmiralRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "AgentRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "CommodoreRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "OperativeRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "PremierRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "ZoneSlot1RoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "ZoneSlot2RoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "ZoneSlot3RoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "ZoneSlot4RoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "ZoneSlot5RoleId",
                table: "GuildSettings");
        }
    }
}
