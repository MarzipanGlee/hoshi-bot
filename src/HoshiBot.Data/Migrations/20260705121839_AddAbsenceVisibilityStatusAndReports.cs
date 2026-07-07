using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAbsenceVisibilityStatusAndReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AbsencesReportMessageId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AbsencesReportStaffMessageId",
                table: "GuildSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Absences",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "EditsAbsenceId",
                table: "Absences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Absences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "Absences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Absences_EditsAbsenceId",
                table: "Absences",
                column: "EditsAbsenceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Absences_Absences_EditsAbsenceId",
                table: "Absences",
                column: "EditsAbsenceId",
                principalTable: "Absences",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Absences_Absences_EditsAbsenceId",
                table: "Absences");

            migrationBuilder.DropIndex(
                name: "IX_Absences_EditsAbsenceId",
                table: "Absences");

            migrationBuilder.DropColumn(
                name: "AbsencesReportMessageId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "AbsencesReportStaffMessageId",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Absences");

            migrationBuilder.DropColumn(
                name: "EditsAbsenceId",
                table: "Absences");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Absences");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Absences");
        }
    }
}
