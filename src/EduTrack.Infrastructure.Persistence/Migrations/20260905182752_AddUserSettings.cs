using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defaults are true so existing users keep their notifications enabled
            // after upgrade (matches the domain default in User.Register).
            migrationBuilder.AddColumn<bool>(
                name: "MorningDigestEnabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "QuietHoursEnd",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuietHoursStart",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Reminder24hEnabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "Reminder2hEnabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MorningDigestEnabled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "QuietHoursEnd",
                table: "users");

            migrationBuilder.DropColumn(
                name: "QuietHoursStart",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Reminder24hEnabled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Reminder2hEnabled",
                table: "users");
        }
    }
}
