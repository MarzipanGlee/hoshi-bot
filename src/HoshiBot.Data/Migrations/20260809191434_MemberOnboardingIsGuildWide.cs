using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// Member Onboarding moves from the Community audience to Guild, where the player linking it
    /// drives has always lived. No schema change — the audience is a column value, so this is purely
    /// the data move for guilds that already enabled it.
    ///
    /// 23 is GuildFeature.MemberOnboarding, 4 is GuildAudience.Community and 16 is Guild, pinned as
    /// literals: both enums are append-only, and a migration must not shift meaning if either is
    /// reordered.
    /// </summary>
    public partial class MemberOnboardingIsGuildWide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"""
                    UPDATE "{table}" SET "Audience" = 16
                    WHERE "Feature" = 23 AND "Audience" = 4;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"""
                    UPDATE "{table}" SET "Audience" = 4
                    WHERE "Feature" = 23 AND "Audience" = 16;
                    """);
            }
        }

        // Everything keyed by (Feature, Audience): whether the feature is on, and its two settings.
        // Miss one and the feature reads as enabled with no configuration, or configured but off.
        private static readonly string[] Tables =
        [
            "GuildEnabledFeatures",
            "GuildFeatureSettingTexts",
            "GuildFeatureSettingSnowflakes",
        ];
    }
}
