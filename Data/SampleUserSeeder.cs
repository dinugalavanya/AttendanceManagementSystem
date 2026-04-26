using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Data
{
    public static class SampleUserSeeder
    {
        private const int SampleUserCount = 50;
        private const int AdminCount = 5;
        private const string DefaultSamplePassword = "User@123";

        public static async Task EnsureSampleUsersAsync(
            ApplicationDbContext context,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            var targetEmails = Enumerable.Range(1, SampleUserCount)
                .Select(i => $"user{i}@test.com")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingEmails = await context.Users
                .AsNoTracking()
                .Where(u => targetEmails.Contains(u.Email))
                .Select(u => u.Email)
                .ToListAsync(cancellationToken);

            if (existingEmails.Count >= SampleUserCount)
            {
                logger.LogInformation("Sample users already exist. Skipping sample user seeding.");
                return;
            }

            var existingEmailSet = existingEmails.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sectionIds = await context.Sections
                .AsNoTracking()
                .Where(s => s.IsActive)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            var usersToInsert = new List<User>();

            for (var i = 1; i <= SampleUserCount; i++)
            {
                var email = $"user{i}@test.com";
                if (existingEmailSet.Contains(email))
                {
                    continue;
                }

                var roleId = i <= AdminCount ? 2 : 3;

                int? sectionId = null;
                if (roleId == 3 && sectionIds.Count > 0)
                {
                    sectionId = sectionIds[(i - 1) % sectionIds.Count];
                }

                usersToInsert.Add(new User
                {
                    FirstName = $"User{i}",
                    LastName = "Sample",
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultSamplePassword),
                    Phone = $"077{i:000000}",
                    Address = $"Sample Address {i}",
                    IsActive = true,
                    RoleId = roleId,
                    SectionId = sectionId
                });
            }

            if (usersToInsert.Count == 0)
            {
                logger.LogInformation("No new sample users to insert.");
                return;
            }

            await context.Users.AddRangeAsync(usersToInsert, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Inserted {Count} sample users into Users table.", usersToInsert.Count);
        }
    }
}
