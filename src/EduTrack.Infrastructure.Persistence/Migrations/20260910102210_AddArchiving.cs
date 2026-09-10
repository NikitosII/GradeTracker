using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "grades",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "grades",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "assignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_grades_StudentUserId_IsArchived",
                table: "grades",
                columns: new[] { "StudentUserId", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_OwnerUserId_IsArchived",
                table: "assignments",
                columns: new[] { "OwnerUserId", "IsArchived" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_grades_StudentUserId_IsArchived",
                table: "grades");

            migrationBuilder.DropIndex(
                name: "IX_assignments_OwnerUserId_IsArchived",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "grades");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "grades");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "assignments");
        }
    }
}
