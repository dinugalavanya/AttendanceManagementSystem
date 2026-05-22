using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AttendanceManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ServiceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LoginTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    LogoutTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    AttendanceStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_AppRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AppRoles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Users_AppSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "AppSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AppAttendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AttendanceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    OutTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalWorkedMinutes = table.Column<int>(type: "int", nullable: false),
                    OvertimeMinutes = table.Column<int>(type: "int", nullable: false),
                    RegularWorkedMinutes = table.Column<int>(type: "int", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAttendances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceEditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceId = table.Column<int>(type: "int", nullable: false),
                    EditedByUserId = table.Column<int>(type: "int", nullable: false),
                    EditReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OldInTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    OldOutTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    OldStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OldTotalWorkedMinutes = table.Column<int>(type: "int", nullable: true),
                    OldOvertimeMinutes = table.Column<int>(type: "int", nullable: true),
                    OldRegularWorkedMinutes = table.Column<int>(type: "int", nullable: true),
                    NewInTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    NewOutTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NewTotalWorkedMinutes = table.Column<int>(type: "int", nullable: true),
                    NewOvertimeMinutes = table.Column<int>(type: "int", nullable: true),
                    NewRegularWorkedMinutes = table.Column<int>(type: "int", nullable: true),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceEditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceEditLogs_AppAttendances_AttendanceId",
                        column: x => x.AttendanceId,
                        principalTable: "AppAttendances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceEditLogs_Users_EditedByUserId",
                        column: x => x.EditedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "AppRoles",
                columns: new[] { "Id", "CreatedAt", "Description", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 5, 13, 5, 55, 3, 354, DateTimeKind.Utc).AddTicks(4489), "Super Administrator with full system access", "SuperAdmin" },
                    { 2, new DateTime(2026, 5, 13, 5, 55, 3, 354, DateTimeKind.Utc).AddTicks(4650), "Section Administrator with limited access", "Admin" },
                    { 3, new DateTime(2026, 5, 13, 5, 55, 3, 354, DateTimeKind.Utc).AddTicks(4651), "Regular worker who can mark attendance", "Worker" }
                });

            migrationBuilder.InsertData(
                table: "AppSections",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(391), "IT Department", true, "Information Technology" },
                    { 2, new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(665), "HR Department", true, "Human Resources" },
                    { 3, new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(666), "Finance and Accounting", true, "Finance" },
                    { 4, new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(668), "Marketing and Sales", true, "Marketing" },
                    { 5, new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(669), "Operations Department", true, "Operations" },
                    { 6, new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(670), "QA Department", true, "Quality Assurance" },
                    { 7, new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(671), "R&D Department", true, "Research & Development" },
                    { 8, new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(672), "Customer Service", true, "Customer Support" },
                    { 9, new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(673), "General Administration", true, "Administration" },
                    { 10, new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(674), "Production Department", true, "Production" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Address", "AttendanceStatus", "Email", "FirstName", "IsActive", "LastName", "LoginTime", "LogoutTime", "PasswordHash", "Phone", "RoleId", "SectionId", "ServiceId", "UpdatedAt" },
                values: new object[] { 1, null, null, "superadmin@attendance.com", "Super", true, "Admin", null, null, "$2a$11$jBmPeOcm/RJmOd4/1nrSjei2PtF7efgQUfPiU6r.RZjh2R0qSmgni", "1234567890", 1, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_AppAttendances_UserId_AttendanceDate",
                table: "AppAttendances",
                columns: new[] { "UserId", "AttendanceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppRoles_Name",
                table: "AppRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEditLogs_AttendanceId",
                table: "AttendanceEditLogs",
                column: "AttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEditLogs_EditedByUserId",
                table: "AttendanceEditLogs",
                column: "EditedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SectionId",
                table: "Users",
                column: "SectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceEditLogs");

            migrationBuilder.DropTable(
                name: "AppAttendances");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "AppRoles");

            migrationBuilder.DropTable(
                name: "AppSections");
        }
    }
}
