using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropShieldRemindersChannelSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Shield Reminders "Channel" setting was written by the editor but never read by
            // any Discord-side consumer (shield alerts go to GuildAlertChannelKind.Shield, owner
            // reminders go by DM). The editor field is gone; delete the orphaned rows so they
            // don't linger in the DB or show up in the Permission Check. Feature 1 =
            // GuildFeature.ShieldReminders.
            migrationBuilder.Sql("""
                DELETE FROM "GuildFeatureSettingSnowflakes" WHERE "Feature" = 1 AND "Key" = 'Channel';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible: the deleted rows were dead data with no consumer, nothing to restore.
        }
    }
}
