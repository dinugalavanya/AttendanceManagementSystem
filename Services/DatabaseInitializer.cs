using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Data;

namespace AttendanceManagementSystem.Services
{
    public class DatabaseMigrationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseMigrationService> _logger;

        public DatabaseMigrationService(ApplicationDbContext context, ILogger<DatabaseMigrationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                // Check if database exists and create if needed
                await _context.Database.EnsureCreatedAsync();

                // Add missing columns to Users table
                await AddMissingUserColumnsAsync();

                _logger.LogInformation("Database initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing database");
                throw;
            }
        }

        private async Task AddMissingUserColumnsAsync()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                // Check and add UpdatedAt column
                if (!await ColumnExistsAsync(connection, "Users", "UpdatedAt"))
                {
                    await ExecuteSqlAsync(connection, "ALTER TABLE Users ADD UpdatedAt DATETIME2 NULL");
                    _logger.LogInformation("Added UpdatedAt column to Users table");
                }

                // Check and add LoginTime column
                if (!await ColumnExistsAsync(connection, "Users", "LoginTime"))
                {
                    await ExecuteSqlAsync(connection, "ALTER TABLE Users ADD LoginTime TIME(7) NULL");
                    _logger.LogInformation("Added LoginTime column to Users table");
                }

                // Check and add LogoutTime column
                if (!await ColumnExistsAsync(connection, "Users", "LogoutTime"))
                {
                    await ExecuteSqlAsync(connection, "ALTER TABLE Users ADD LogoutTime TIME(7) NULL");
                    _logger.LogInformation("Added LogoutTime column to Users table");
                }

                // Check and add AttendanceStatus column
                if (!await ColumnExistsAsync(connection, "Users", "AttendanceStatus"))
                {
                    await ExecuteSqlAsync(connection, "ALTER TABLE Users ADD AttendanceStatus NVARCHAR(20) NULL");
                    _logger.LogInformation("Added AttendanceStatus column to Users table");
                }

                await connection.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding missing columns to Users table");
                throw;
            }
        }

        private async Task<bool> ColumnExistsAsync(System.Data.Common.DbConnection connection, string tableName, string columnName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName";

            var tableNameParam = command.CreateParameter();
            tableNameParam.ParameterName = "@TableName";
            tableNameParam.Value = tableName;
            command.Parameters.Add(tableNameParam);

            var columnNameParam = command.CreateParameter();
            columnNameParam.ParameterName = "@ColumnName";
            columnNameParam.Value = columnName;
            command.Parameters.Add(columnNameParam);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        private async Task ExecuteSqlAsync(System.Data.Common.DbConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }
}
