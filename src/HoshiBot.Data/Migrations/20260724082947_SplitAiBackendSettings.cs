using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitAiBackendSettings : Migration
    {
        // Data-only migration (no schema change): splits AiChat's guild-wide backend scalars out to
        // the new AiBackend feature, and fans the per-audience behavioral scalars out into each
        // enabled AiChat audience. Enum values are inlined as integers on purpose — a migration is a
        // historical snapshot and must not depend on the current enum's ordinals drifting later.
        //   GuildFeature: AiChat=17, MemberLore=21, AnnouncementForwarder=26, AiBackend=27
        //   GuildAudience: None=0, Alliance=1, Guild=16
        // Backend keys (move guild-wide): ApiKey, Provider, Model, GateModel, RouterModel,
        //   MemberLoreModel, EmbeddingProvider.
        // Behavioral keys (fan out per audience): SystemPrompt, StreamResponses, MemoryEnabled,
        //   SearchLanguage. MemoryWatermark stays guild-wide under (AiChat, None) untouched.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Move the backend scalars from (AiChat, None) to (AiBackend, Guild). GuildAllianceId
            //    was null and stays null (Guild audience requires null).
            migrationBuilder.Sql("""
                UPDATE "GuildFeatureSettingTexts"
                SET "Feature" = 27, "Audience" = 16
                WHERE "Feature" = 17 AND "Audience" = 0
                  AND "Key" IN ('ApiKey','Provider','Model','GateModel','RouterModel','MemberLoreModel','EmbeddingProvider');
                """);

            // 2) Fan the behavioral scalars out from (AiChat, None) into every audience the guild has
            //    AiChat enabled under (carrying that enable row's GuildAllianceId), then drop the None
            //    originals. NOT EXISTS guards against a duplicate on any pre-existing per-audience row.
            migrationBuilder.Sql("""
                INSERT INTO "GuildFeatureSettingTexts" ("GuildId","Feature","Audience","GuildAllianceId","Key","Value")
                SELECT t."GuildId", 17, ef."Audience", ef."GuildAllianceId", t."Key", t."Value"
                FROM "GuildFeatureSettingTexts" t
                JOIN "GuildEnabledFeatures" ef
                  ON ef."GuildId" = t."GuildId" AND ef."Feature" = 17
                WHERE t."Feature" = 17 AND t."Audience" = 0
                  AND t."Key" IN ('SystemPrompt','StreamResponses','MemoryEnabled','SearchLanguage')
                  AND NOT EXISTS (
                      SELECT 1 FROM "GuildFeatureSettingTexts" x
                      WHERE x."GuildId" = t."GuildId" AND x."Feature" = 17 AND x."Audience" = ef."Audience"
                        AND ((x."GuildAllianceId" IS NULL AND ef."GuildAllianceId" IS NULL) OR x."GuildAllianceId" = ef."GuildAllianceId")
                        AND x."Key" = t."Key"
                  );
                """);
            migrationBuilder.Sql("""
                DELETE FROM "GuildFeatureSettingTexts"
                WHERE "Feature" = 17 AND "Audience" = 0
                  AND "Key" IN ('SystemPrompt','StreamResponses','MemoryEnabled','SearchLanguage');
                """);

            // 3) Enable AiBackend (Guild audience) for every guild that already has any AI-powered
            //    feature enabled (AiChat, MemberLore, or AnnouncementForwarder), so existing guilds
            //    keep working without a manual re-enable.
            migrationBuilder.Sql("""
                INSERT INTO "GuildEnabledFeatures" ("GuildId","Feature","Audience","GuildAllianceId")
                SELECT DISTINCT ef."GuildId", 27, 16, NULL::integer
                FROM "GuildEnabledFeatures" ef
                WHERE ef."Feature" IN (17, 21, 26)
                  AND NOT EXISTS (
                      SELECT 1 FROM "GuildEnabledFeatures" x
                      WHERE x."GuildId" = ef."GuildId" AND x."Feature" = 27
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort reverse. Fanning per-audience values back to one guild-wide value is lossy
            // (multiple audiences collapse to one) — DISTINCT ON keeps one per (guild, key).
            migrationBuilder.Sql("DELETE FROM \"GuildEnabledFeatures\" WHERE \"Feature\" = 27;");

            migrationBuilder.Sql("""
                UPDATE "GuildFeatureSettingTexts"
                SET "Feature" = 17, "Audience" = 0
                WHERE "Feature" = 27 AND "Audience" = 16
                  AND "Key" IN ('ApiKey','Provider','Model','GateModel','RouterModel','MemberLoreModel','EmbeddingProvider');
                """);

            migrationBuilder.Sql("""
                INSERT INTO "GuildFeatureSettingTexts" ("GuildId","Feature","Audience","GuildAllianceId","Key","Value")
                SELECT DISTINCT ON (t."GuildId", t."Key") t."GuildId", 17, 0, NULL::integer, t."Key", t."Value"
                FROM "GuildFeatureSettingTexts" t
                WHERE t."Feature" = 17 AND t."Audience" <> 0
                  AND t."Key" IN ('SystemPrompt','StreamResponses','MemoryEnabled','SearchLanguage')
                  AND NOT EXISTS (
                      SELECT 1 FROM "GuildFeatureSettingTexts" x
                      WHERE x."GuildId" = t."GuildId" AND x."Feature" = 17 AND x."Audience" = 0
                        AND x."GuildAllianceId" IS NULL AND x."Key" = t."Key"
                  );
                """);
            migrationBuilder.Sql("""
                DELETE FROM "GuildFeatureSettingTexts"
                WHERE "Feature" = 17 AND "Audience" <> 0
                  AND "Key" IN ('SystemPrompt','StreamResponses','MemoryEnabled','SearchLanguage');
                """);
        }
    }
}
