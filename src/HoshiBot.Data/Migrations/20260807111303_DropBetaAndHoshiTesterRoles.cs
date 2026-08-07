using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <summary>
    /// Both roles came across from the legacy bot and neither gated anything in either bot.
    ///
    /// BetaTester's only use, there and here, was the staff bridge's own "manage beta tests" button
    /// adding or removing it from the caller — a self-service toggle for a role no code ever read
    /// (whatever it unlocked was a Discord channel permission, set outside the bot). That button and
    /// its service go with this.
    ///
    /// HoshiTester was never referenced at all: legacy defined the role and then only ever used a
    /// separate hardcoded USER list of the same name, inside a block commented out and marked
    /// "TODO remove after testing".
    ///
    /// A plain drop, unlike MoveBotSupportChannelToFeature: there is no feature to carry the values
    /// to, because there is no feature.
    /// </summary>
    public partial class DropBetaAndHoshiTesterRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BetaTesterRoleId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "HoshiTesterRoleId",
                table: "GuildSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BetaTesterRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HoshiTesterRoleId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);
        }
    }
}
