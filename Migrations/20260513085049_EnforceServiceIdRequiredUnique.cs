using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class EnforceServiceIdRequiredUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [Users]
                SET [ServiceId] = NULLIF(UPPER(LTRIM(RTRIM([ServiceId]))), '')
                """);

            migrationBuilder.Sql(
                """
                UPDATE [Users]
                SET [ServiceId] = CONCAT('EMP', RIGHT('000' + CAST([Id] AS varchar(10)), 3))
                WHERE [ServiceId] IS NULL
                """);

            migrationBuilder.Sql(
                """
                ;WITH DuplicateServiceIds AS
                (
                    SELECT [Id], [ServiceId],
                           ROW_NUMBER() OVER (PARTITION BY [ServiceId] ORDER BY [Id]) AS [RowNum]
                    FROM [Users]
                )
                UPDATE [Users]
                SET [ServiceId] = CONCAT('EMP-ID', CAST([Users].[Id] AS varchar(10)))
                FROM [Users]
                INNER JOIN [DuplicateServiceIds] ON [Users].[Id] = [DuplicateServiceIds].[Id]
                WHERE [DuplicateServiceIds].[RowNum] > 1
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ServiceId",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "ServiceId",
                value: "EMP900");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ServiceId",
                table: "Users",
                column: "ServiceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_ServiceId",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "ServiceId",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "ServiceId",
                value: null);
        }
    }
}
