using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class ForwarderAndReadReceiptsBecomeAudienceScoped : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Announcement Forwarder and Read Confirmation move from the guild-wide audience to the
            // per-audience set. Guild is not a selectable audience, so rows left at 16 would be
            // invisible in the admin and dead at runtime — they have to be carried over.
            //
            // Only guilds with EXACTLY ONE linked alliance are moved, because only there is
            // "which alliance" answerable. Every guild on this deployment has one. A guild with none
            // or several keeps its rows at the old audience: inert, but visible in the database and
            // recoverable by hand, which beats guessing an alliance or deleting a configured channel.
            const string OneAllianceGuild = """
                SELECT ga."GuildId", min(ga."Id") AS alliance_id
                FROM "GuildAlliances" ga
                GROUP BY ga."GuildId"
                HAVING count(*) = 1
                """;

            foreach (var table in new[] { "GuildEnabledFeatures", "GuildFeatureSettingSnowflakes", "GuildFeatureSettingTexts" })
            {
                migrationBuilder.Sql($"""
                    UPDATE "{table}" t
                    SET "Audience" = 1, "GuildAllianceId" = g.alliance_id
                    FROM ({OneAllianceGuild}) g
                    WHERE t."GuildId" = g."GuildId"
                      AND t."Feature" IN (26, 37)
                      AND t."Audience" = 16;
                    """);
            }

            // GuildFeatureChannels carries no alliance id by design — the source channels a forwarder
            // watches are shared across a guild's alliances — so only the audience moves.
            migrationBuilder.Sql($"""
                UPDATE "GuildFeatureChannels" c
                SET "Audience" = 1
                FROM ({OneAllianceGuild}) g
                WHERE c."GuildId" = g."GuildId"
                  AND c."Feature" = 26
                  AND c."Audience" = 16;
                """);

            // Posts already registered were made under the guild-wide scope. They keep their own
            // ReadReceiptsEnabled flag, which is the point of storing it — nothing about this
            // re-scoping changes what a member is already being asked to confirm.

            migrationBuilder.DropIndex(
                name: "IX_ForwardedAnnouncements_SourceMessageId",
                table: "ForwardedAnnouncements");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardedAnnouncements_SourceMessageId",
                table: "ForwardedAnnouncements",
                column: "SourceMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardedAnnouncements_SourceMessageId_DestinationChannelId",
                table: "ForwardedAnnouncements",
                columns: new[] { "SourceMessageId", "DestinationChannelId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "GuildEnabledFeatures", "GuildFeatureSettingSnowflakes", "GuildFeatureSettingTexts" })
            {
                migrationBuilder.Sql($"""
                    UPDATE "{table}" SET "Audience" = 16, "GuildAllianceId" = NULL
                    WHERE "Feature" IN (26, 37) AND "Audience" = 1;
                    """);
            }

            migrationBuilder.Sql("""
                UPDATE "GuildFeatureChannels" SET "Audience" = 16 WHERE "Feature" = 26 AND "Audience" = 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ForwardedAnnouncements_SourceMessageId",
                table: "ForwardedAnnouncements");

            migrationBuilder.DropIndex(
                name: "IX_ForwardedAnnouncements_SourceMessageId_DestinationChannelId",
                table: "ForwardedAnnouncements");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardedAnnouncements_SourceMessageId",
                table: "ForwardedAnnouncements",
                column: "SourceMessageId",
                unique: true);
        }
    }
}
