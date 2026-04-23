using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Data
{
    public static class DatabaseInitializer
    {
        public const string DefaultSuperAdminEmail = "superadmin@attendance.com";
        public const string DefaultSuperAdminPassword = "Admin@123";

        public static async Task EnsureCoreDataAsync(
            ApplicationDbContext context,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);

            await EnsureRolesAsync(context, cancellationToken);
            await EnsureSectionsAsync(context, cancellationToken);
            await EnsureSuperAdminAsync(context, logger, cancellationToken);
        }

        private static async Task EnsureRolesAsync(ApplicationDbContext context, CancellationToken cancellationToken)
        {
            var requiredRoles = new[]
            {
                new Role { Name = RoleNames.SuperAdmin, Description = "Super Administrator with full system access" },
                new Role { Name = RoleNames.Admin, Description = "Section Administrator with limited access" },
                new Role { Name = RoleNames.Worker, Description = "Regular worker who can mark attendance" }
            };

            foreach (var role in requiredRoles)
            {
                var exists = await context.Roles.AnyAsync(r => r.Name == role.Name, cancellationToken);
                if (!exists)
                {
                    role.CreatedAt = DateTime.UtcNow;
                    context.Roles.Add(role);
                }
            }

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private static async Task EnsureSectionsAsync(ApplicationDbContext context, CancellationToken cancellationToken)
        {
            if (await context.Sections.AnyAsync(cancellationToken))
            {
                return;
            }

            var sections = new[]
            {
                new Section { Name = "Information Technology", Description = "IT Department", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Section { Name = "Human Resources", Description = "HR Department", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Section { Name = "Finance", Description = "Finance and Accounting", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Section { Name = "Marketing", Description = "Marketing and Sales", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Section { Name = "Operations", Description = "Operations Department", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Section { Name = "Quality Assurance", Description = "QA Department", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Section { Name = "Research & Development", Description = "R&D Department", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Section { Name = "Customer Support", Description = "Customer Service", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Section { Name = "Administration", Description = "General Administration", IsActive = true, CreatedAt = DateTime.UtcNow },
                new Section { Name = "Production", Description = "Production Department", IsActive = true, CreatedAt = DateTime.UtcNow }
            };

            await context.Sections.AddRangeAsync(sections, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        private static async Task EnsureSuperAdminAsync(
            ApplicationDbContext context,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var superAdminRoleId = await context.Roles
                .Where(r => r.Name == RoleNames.SuperAdmin)
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (superAdminRoleId == 0)
            {
                throw new InvalidOperationException("SuperAdmin role is missing and could not be resolved.");
            }

            var superAdmin = await context.Users
                .FirstOrDefaultAsync(
                    u => u.Email.ToLower() == DefaultSuperAdminEmail.ToLower(),
                    cancellationToken);

            if (superAdmin == null)
            {
                context.Users.Add(new User
                {
                    FirstName = "Super",
                    LastName = "Admin",
                    Email = DefaultSuperAdminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultSuperAdminPassword),
                    Phone = "1234567890",
                    RoleId = superAdminRoleId,
                    SectionId = null,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Default super admin user was created.");
                return;
            }

            var shouldUpdate = false;

            if (superAdmin.RoleId != superAdminRoleId)
            {
                superAdmin.RoleId = superAdminRoleId;
                shouldUpdate = true;
            }

            if (!superAdmin.IsActive)
            {
                superAdmin.IsActive = true;
                shouldUpdate = true;
            }

            var passwordMatches = TryVerifyPassword(superAdmin.PasswordHash, DefaultSuperAdminPassword);
            if (!passwordMatches)
            {
                superAdmin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultSuperAdminPassword);
                shouldUpdate = true;
            }

            if (!shouldUpdate)
            {
                return;
            }

            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Default super admin account was normalized for login.");
        }

        private static bool TryVerifyPassword(string existingHash, string password)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, existingHash);
            }
            catch
            {
                return false;
            }
        }
    }
}
