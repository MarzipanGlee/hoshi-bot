using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConditionalRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConditionalRoleConditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConditionalRoleConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConditionalRoleConditions_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConditionalRoleRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetRoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConditionalRoleRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConditionalRoleRules_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConditionalRoleNodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerRuleId = table.Column<int>(type: "integer", nullable: true),
                    OwnerConditionId = table.Column<int>(type: "integer", nullable: true),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ReferencedConditionId = table.Column<int>(type: "integer", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConditionalRoleNodes", x => x.Id);
                    table.CheckConstraint("CK_ConditionalRoleNodes_SingleOwner", "(\"OwnerRuleId\" IS NULL) <> (\"OwnerConditionId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ConditionalRoleNodes_ConditionalRoleConditions_OwnerConditi~",
                        column: x => x.OwnerConditionId,
                        principalTable: "ConditionalRoleConditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConditionalRoleNodes_ConditionalRoleConditions_ReferencedCo~",
                        column: x => x.ReferencedConditionId,
                        principalTable: "ConditionalRoleConditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConditionalRoleNodes_ConditionalRoleNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ConditionalRoleNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConditionalRoleNodes_ConditionalRoleRules_OwnerRuleId",
                        column: x => x.OwnerRuleId,
                        principalTable: "ConditionalRoleRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConditionalRoleConditions_GuildId_Name",
                table: "ConditionalRoleConditions",
                columns: new[] { "GuildId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConditionalRoleNodes_OwnerConditionId",
                table: "ConditionalRoleNodes",
                column: "OwnerConditionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConditionalRoleNodes_OwnerRuleId",
                table: "ConditionalRoleNodes",
                column: "OwnerRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ConditionalRoleNodes_ParentId",
                table: "ConditionalRoleNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConditionalRoleNodes_ReferencedConditionId",
                table: "ConditionalRoleNodes",
                column: "ReferencedConditionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConditionalRoleRules_GuildId",
                table: "ConditionalRoleRules",
                column: "GuildId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConditionalRoleNodes");

            migrationBuilder.DropTable(
                name: "ConditionalRoleConditions");

            migrationBuilder.DropTable(
                name: "ConditionalRoleRules");
        }
    }
}
