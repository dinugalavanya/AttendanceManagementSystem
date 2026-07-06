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
                _logger.LogInformation("Applying EF Core migrations for the application database...");
                await _context.Database.MigrateAsync();
                _logger.LogInformation("EF Core migrations applied successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database migration failed: {Message}", ex.Message);
                throw;
            }
        }
    }
}
