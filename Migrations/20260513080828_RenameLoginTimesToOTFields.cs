using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class RenameLoginTimesToOTFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LogoutTime",
                table: "Users",
                newName: "OTLogoutTime");

            migrationBuilder.RenameColumn(
                name: "LoginTime",
                table: "Users",
                newName: "OTLoginTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OTLogoutTime",
                table: "Users",
                newName: "LogoutTime");

            migrationBuilder.RenameColumn(
                name: "OTLoginTime",
                table: "Users",
                newName: "LoginTime");
        }
    }
}
