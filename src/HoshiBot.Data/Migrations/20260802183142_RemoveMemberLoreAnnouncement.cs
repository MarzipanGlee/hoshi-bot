using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// Data-only: drops the member-lore "Announcement" rows. The setting held a channel post admins
    /// were meant to copy into a channel by hand; nothing in the bot ever read it, and the key no
    /// longer exists in code (MemberLoreSettingKeys), so the rows are pure leftovers.
    ///
    /// GuildFeature is stored as its enum ordinal, which the enum's own comments pin ("keep last so
    /// existing enum ordinals/DB rows don't shift"): MemberLore = 21.
    /// </summary>
    public partial class RemoveMemberLoreAnnouncement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "GuildFeatureSettingTexts" WHERE "Feature" = 21 AND "Key" = 'Announcement';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: the rows were a copy-paste template no code path consumed, and the
            // text they held is gone from the codebase as well.
        }
    }
}
