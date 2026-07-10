using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGenericFeatureSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Audience",
                table: "GuildAlertChannels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GuildEnabledFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Feature = table.Column<int>(type: "integer", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildEnabledFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildEnabledFeatures_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildFeatureSettingSnowflakes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Feature = table.Column<int>(type: "integer", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildFeatureSettingSnowflakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildFeatureSettingSnowflakes_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildFeatureSettingTexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Feature = table.Column<int>(type: "integer", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildFeatureSettingTexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildFeatureSettingTexts_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildEnabledFeatures_GuildId_Feature_Audience",
                table: "GuildEnabledFeatures",
                columns: new[] { "GuildId", "Feature", "Audience" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildId_Feature_Audience_Key",
                table: "GuildFeatureSettingSnowflakes",
                columns: new[] { "GuildId", "Feature", "Audience", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingSnowflakes_GuildId_Feature_Audience_Key_~",
                table: "GuildFeatureSettingSnowflakes",
                columns: new[] { "GuildId", "Feature", "Audience", "Key", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildFeatureSettingTexts_GuildId_Feature_Audience_Key",
                table: "GuildFeatureSettingTexts",
                columns: new[] { "GuildId", "Feature", "Audience", "Key" },
                unique: true);

            // Behavior-preserving backfill for the GuildDisabledFeature -> GuildEnabledFeature
            // polarity flip: every existing guild must keep its current effective on/off
            // state despite "presence = enabled" replacing "presence = disabled" as the
            // default. For every (GuildId, Feature, Audience) combination that applies
            // today, insert a GuildEnabledFeature row UNLESS the guild had an explicit
            // GuildDisabledFeatures row for that Feature (which, before this migration,
            // disabled it for every audience at once). Going forward, a brand-new guild (or
            // a future feature) starts fully off until an admin opts in — only this one-time
            // backfill treats "no row yet" as "was on."
            //
            // Single-audience features (RaidAlerts=0, ShieldReminders=1,
            // TerritoryCapture=2, RoeViolationReports=6, Absences=7, AlertsOptIn=8,
            // Diplomacy=9) always resolve to Audience=Alliance(1) - see
            // GuildFeatureAudiences.RelevantAudiences.
            migrationBuilder.Sql(
                """
                INSERT INTO "GuildEnabledFeatures" ("GuildId", "Feature", "Audience")
                SELECT g."Id", v."Feature", 1
                FROM "DiscordGuilds" g
                CROSS JOIN (VALUES (0),(1),(2),(6),(7),(8),(9)) AS v("Feature")
                WHERE NOT EXISTS (
                    SELECT 1 FROM "GuildDisabledFeatures" d
                    WHERE d."GuildId" = g."Id" AND d."Feature" = v."Feature"
                );
                """);

            // 3-way features (Announcements=3, Tickets=4, AnonymousMessaging=5): one row
            // per audience bit set on GuildSettings.Audiences, falling back to
            // Community(4) when the guild has none of the 3 bits set (hasn't configured an
            // audience yet) - Community is the most "catch-all" of the three.
            migrationBuilder.Sql(
                """
                INSERT INTO "GuildEnabledFeatures" ("GuildId", "Feature", "Audience")
                SELECT g."Id", v."Feature", 1
                FROM "DiscordGuilds" g
                LEFT JOIN "GuildSettings" s ON s."GuildId" = g."Id"
                CROSS JOIN (VALUES (3),(4),(5)) AS v("Feature")
                WHERE (COALESCE(s."Audiences", 0) & 1) != 0
                  AND NOT EXISTS (SELECT 1 FROM "GuildDisabledFeatures" d WHERE d."GuildId" = g."Id" AND d."Feature" = v."Feature");
                """);
            migrationBuilder.Sql(
                """
                INSERT INTO "GuildEnabledFeatures" ("GuildId", "Feature", "Audience")
                SELECT g."Id", v."Feature", 2
                FROM "DiscordGuilds" g
                LEFT JOIN "GuildSettings" s ON s."GuildId" = g."Id"
                CROSS JOIN (VALUES (3),(4),(5)) AS v("Feature")
                WHERE (COALESCE(s."Audiences", 0) & 2) != 0
                  AND NOT EXISTS (SELECT 1 FROM "GuildDisabledFeatures" d WHERE d."GuildId" = g."Id" AND d."Feature" = v."Feature");
                """);
            migrationBuilder.Sql(
                """
                INSERT INTO "GuildEnabledFeatures" ("GuildId", "Feature", "Audience")
                SELECT g."Id", v."Feature", 4
                FROM "DiscordGuilds" g
                LEFT JOIN "GuildSettings" s ON s."GuildId" = g."Id"
                CROSS JOIN (VALUES (3),(4),(5)) AS v("Feature")
                WHERE ((COALESCE(s."Audiences", 0) & 4) != 0 OR (COALESCE(s."Audiences", 0) & 7) = 0)
                  AND NOT EXISTS (SELECT 1 FROM "GuildDisabledFeatures" d WHERE d."GuildId" = g."Id" AND d."Feature" = v."Feature");
                """);

            // 2-way features (ServerStatus=10, Incursion=11): one row per audience bit set,
            // falling back to ServerVeilGroup(2) when the guild has neither Alliance nor
            // ServerVeilGroup set (Community isn't a valid audience for these two).
            migrationBuilder.Sql(
                """
                INSERT INTO "GuildEnabledFeatures" ("GuildId", "Feature", "Audience")
                SELECT g."Id", v."Feature", 1
                FROM "DiscordGuilds" g
                LEFT JOIN "GuildSettings" s ON s."GuildId" = g."Id"
                CROSS JOIN (VALUES (10),(11)) AS v("Feature")
                WHERE (COALESCE(s."Audiences", 0) & 1) != 0
                  AND NOT EXISTS (SELECT 1 FROM "GuildDisabledFeatures" d WHERE d."GuildId" = g."Id" AND d."Feature" = v."Feature");
                """);
            migrationBuilder.Sql(
                """
                INSERT INTO "GuildEnabledFeatures" ("GuildId", "Feature", "Audience")
                SELECT g."Id", v."Feature", 2
                FROM "DiscordGuilds" g
                LEFT JOIN "GuildSettings" s ON s."GuildId" = g."Id"
                CROSS JOIN (VALUES (10),(11)) AS v("Feature")
                WHERE ((COALESCE(s."Audiences", 0) & 2) != 0 OR (COALESCE(s."Audiences", 0) & 3) = 0)
                  AND NOT EXISTS (SELECT 1 FROM "GuildDisabledFeatures" d WHERE d."GuildId" = g."Id" AND d."Feature" = v."Feature");
                """);

            // GuildAlertChannel rows predate the Audience column - assign existing rows in
            // place rather than duplicating them (duplicating would double-send
            // notifications). Raid=0/Shield=1 rows are Alliance-only features, always
            // Alliance(1); ServerStatus=2/Incursion=3 rows default to the ServerVeilGroup(2)
            // fallback - admins can re-tag/split via the editor afterward.
            migrationBuilder.Sql(
                """
                UPDATE "GuildAlertChannels" SET "Audience" = 1 WHERE "Kind" IN (0, 1);
                """);
            migrationBuilder.Sql(
                """
                UPDATE "GuildAlertChannels" SET "Audience" = 2 WHERE "Kind" IN (2, 3);
                """);

            // Safe to drop now - every row's effective on/off state has been materialized
            // into GuildEnabledFeatures above.
            migrationBuilder.DropTable(
                name: "GuildDisabledFeatures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildEnabledFeatures");

            migrationBuilder.DropTable(
                name: "GuildFeatureSettingSnowflakes");

            migrationBuilder.DropTable(
                name: "GuildFeatureSettingTexts");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "GuildAlertChannels");

            migrationBuilder.CreateTable(
                name: "GuildDisabledFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Feature = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildDisabledFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildDisabledFeatures_DiscordGuilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "DiscordGuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildDisabledFeatures_GuildId_Feature",
                table: "GuildDisabledFeatures",
                columns: new[] { "GuildId", "Feature" },
                unique: true);
        }
    }
}
