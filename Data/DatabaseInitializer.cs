using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Data
{
    public static class DatabaseInitializer
    {
        public const string DefaultSuperAdminEmail = "superadmin@attendance.com";
        public const string DefaultSuperAdminPassword = "Admin@123";
        public const string DefaultAdminEmail = "tvvithana@gmail.com";
        public const string DefaultAdminPassword = "Din@yak!23";
        public const string DefaultWorkerEmail = "yenu@gmail.com";
        public const string DefaultWorkerPassword = "123";
        public const string DefaultMarketingSectionName = "Marketing";
        public const string DefaultSuperAdminServiceId = "EMP900";
        public const string DefaultAdminServiceId = "EMP901";
        public const string DefaultWorkerServiceId = "EMP001";

        public static async Task EnsureCoreDataAsync(
            ApplicationDbContext context,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            await EnsureRolesAsync(context, cancellationToken);
            var marketingSectionId = await EnsureMarketingSectionAsync(context, cancellationToken);
            await EnsureSuperAdminAsync(context, logger, cancellationToken);
            await EnsureAdminAndWorkerAsync(context, logger, marketingSectionId, cancellationToken);
            await EnsureNewRoleUsersAsync(context, logger, marketingSectionId, cancellationToken);
            await EnsureSriLankanWorkersAsync(context, logger, cancellationToken);
            await EnsureDummyAttendanceAsync(context, logger, cancellationToken);
        }

        private static async Task EnsureRolesAsync(ApplicationDbContext context, CancellationToken cancellationToken)
        {
            var requiredRoles = new[]
            {
                new Role { Name = RoleNames.SuperAdmin, Description = "Super Administrator with full system access" },
                new Role { Name = RoleNames.Admin, Description = "Section Administrator with limited access" },
                new Role { Name = RoleNames.GM, Description = "General Manager - view all sections" },
                new Role { Name = RoleNames.DGM, Description = "Deputy General Manager - view own section only" },
                new Role { Name = RoleNames.Engineer, Description = "Engineer - view and edit own section" },
                new Role { Name = RoleNames.Worker, Description = "Regular worker who can enter OT data" }
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

        private static async Task<int> EnsureMarketingSectionAsync(ApplicationDbContext context, CancellationToken cancellationToken)
        {
            var marketingSection = await context.Sections
                .FirstOrDefaultAsync(s => s.Name.ToLower() == DefaultMarketingSectionName.ToLower(), cancellationToken);

            if (marketingSection == null)
            {
                marketingSection = new Section
                {
                    Name = DefaultMarketingSectionName,
                    Description = "Marketing and Sales",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.Sections.Add(marketingSection);
                await context.SaveChangesAsync(cancellationToken);
                return marketingSection.Id;
            }

            if (!marketingSection.IsActive || !string.Equals(marketingSection.Description, "Marketing and Sales", StringComparison.Ordinal))
            {
                marketingSection.IsActive = true;
                marketingSection.Description = "Marketing and Sales";
                await context.SaveChangesAsync(cancellationToken);
            }

            return marketingSection.Id;
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
                var serviceId = await ResolveUniqueServiceIdAsync(
                    context,
                    DefaultSuperAdminServiceId,
                    null,
                    cancellationToken);

                context.Users.Add(new User
                {
                    FirstName = "Super",
                    LastName = "Admin",
                    Email = DefaultSuperAdminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultSuperAdminPassword),
                    Phone = "1234567890",
                    ServiceId = serviceId,
                    RoleId = superAdminRoleId,
                    SectionId = null,
                    IsActive = true
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

            if (superAdmin.SectionId != null)
            {
                superAdmin.SectionId = null;
                shouldUpdate = true;
            }

            var desiredSuperAdminServiceId = await ResolveUniqueServiceIdAsync(
                context,
                DefaultSuperAdminServiceId,
                superAdmin.Id,
                cancellationToken);
            if (!string.Equals(superAdmin.ServiceId, desiredSuperAdminServiceId, StringComparison.Ordinal))
            {
                superAdmin.ServiceId = desiredSuperAdminServiceId;
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

        private static async Task EnsureAdminAndWorkerAsync(
            ApplicationDbContext context,
            ILogger logger,
            int marketingSectionId,
            CancellationToken cancellationToken)
        {
            var adminRoleId = await context.Roles
                .Where(r => r.Name == RoleNames.Admin)
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var workerRoleId = await context.Roles
                .Where(r => r.Name == RoleNames.Worker)
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (adminRoleId == 0 || workerRoleId == 0)
            {
                throw new InvalidOperationException("Admin/Worker roles are missing and could not be resolved.");
            }

            await EnsureUserAsync(
                context,
                email: DefaultAdminEmail,
                password: DefaultAdminPassword,
                firstName: "Dinuga",
                lastName: "Vithana",
                phone: "0000000000",
                preferredServiceId: DefaultAdminServiceId,
                roleId: adminRoleId,
                sectionId: marketingSectionId,
                cancellationToken);

            await EnsureUserAsync(
                context,
                email: DefaultWorkerEmail,
                password: DefaultWorkerPassword,
                firstName: "Yenu",
                lastName: "Worker",
                phone: "0000000001",
                preferredServiceId: DefaultWorkerServiceId,
                roleId: workerRoleId,
                sectionId: marketingSectionId,
                cancellationToken);

            logger.LogInformation("Default admin and worker users were ensured for login.");
        }

        private static async Task EnsureUserAsync(
            ApplicationDbContext context,
            string email,
            string password,
            string firstName,
            string lastName,
            string phone,
            string preferredServiceId,
            int roleId,
            int? sectionId,
            CancellationToken cancellationToken)
        {
            var user = await context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

            if (user == null)
            {
                var resolvedServiceId = await ResolveUniqueServiceIdAsync(
                    context,
                    preferredServiceId,
                    null,
                    cancellationToken);

                context.Users.Add(new User
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    Phone = phone,
                    ServiceId = resolvedServiceId,
                    RoleId = roleId,
                    SectionId = sectionId,
                    IsActive = true
                });

                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            var shouldUpdate = false;

            if (!string.Equals(user.FirstName, firstName, StringComparison.Ordinal))
            {
                user.FirstName = firstName;
                shouldUpdate = true;
            }

            if (!string.Equals(user.LastName, lastName, StringComparison.Ordinal))
            {
                user.LastName = lastName;
                shouldUpdate = true;
            }

            if (!string.Equals(user.Phone, phone, StringComparison.Ordinal))
            {
                user.Phone = phone;
                shouldUpdate = true;
            }

            if (user.RoleId != roleId)
            {
                user.RoleId = roleId;
                shouldUpdate = true;
            }

            if (user.SectionId != sectionId)
            {
                user.SectionId = sectionId;
                shouldUpdate = true;
            }

            if (!user.IsActive)
            {
                user.IsActive = true;
                shouldUpdate = true;
            }

            var resolvedExistingServiceId = await ResolveUniqueServiceIdAsync(
                context,
                preferredServiceId,
                user.Id,
                cancellationToken);
            if (!string.Equals(user.ServiceId, resolvedExistingServiceId, StringComparison.Ordinal))
            {
                user.ServiceId = resolvedExistingServiceId;
                shouldUpdate = true;
            }

            if (!TryVerifyPassword(user.PasswordHash, password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                shouldUpdate = true;
            }

            if (!shouldUpdate)
            {
                return;
            }

            await context.SaveChangesAsync(cancellationToken);
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

        private static async Task EnsureSriLankanWorkersAsync(
            ApplicationDbContext context,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var workerRoleId = await context.Roles
                .Where(r => r.Name == RoleNames.Worker)
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var sections = await context.Sections.ToListAsync(cancellationToken);
            var existingUsersByEmail = await context.Users
                .ToDictionaryAsync(u => u.Email.ToLower(), cancellationToken);

            // Track service IDs already in use (from DB) and those we assign during seeding
            var takenServiceIds = existingUsersByEmail.Values
                .Select(u => (u.ServiceId ?? string.Empty).ToUpperInvariant())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToHashSet(StringComparer.Ordinal);

            async Task<string> GetAvailableServiceIdAsync(string preferred, int? currentUserId)
            {
                var normalized = (preferred ?? string.Empty).Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    normalized = BuildEmployeeServiceId(1);
                }

                // If preferred is free in DB and not yet taken in this run, use it
                var preferredUsedByOther = await context.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.ServiceId != null && u.ServiceId.ToUpper() == normalized && (!currentUserId.HasValue || u.Id != currentUserId.Value), cancellationToken);

                if (!preferredUsedByOther && !takenServiceIds.Contains(normalized))
                {
                    takenServiceIds.Add(normalized);
                    return normalized;
                }

                // Start searching from the preferred number if it looks like EMP###
                var startNumber = 1;
                if (normalized.StartsWith("EMP", StringComparison.Ordinal) && int.TryParse(normalized[3..], out var parsed) && parsed > 0)
                {
                    startNumber = parsed + 1;
                }

                var candidateNumber = Math.Max(1, startNumber);
                while (true)
                {
                    var candidate = BuildEmployeeServiceId(candidateNumber);
                    var inDb = await context.Users
                        .AsNoTracking()
                        .AnyAsync(u => u.ServiceId != null && u.ServiceId.ToUpper() == candidate && (!currentUserId.HasValue || u.Id != currentUserId.Value), cancellationToken);

                    if (!inDb && !takenServiceIds.Contains(candidate))
                    {
                        takenServiceIds.Add(candidate);
                        return candidate;
                    }

                    candidateNumber++;
                }
            }

            if (workerRoleId == 0 || sections.Count == 0)
            {
                logger.LogWarning("Cannot seed workers: Worker role or sections not found.");
                return;
            }

            var sriLankanWorkers = new[]
            {
                ("Dinuka", "Perera", "+94771234567", "123 Colombo Street, Colombo 7"),
                ("Sarath", "Silva", "+94712345678", "456 Galle Road, Galle"),
                ("Priya", "Kumari", "+94763456789", "789 Main Street, Kandy"),
                ("Ravi", "Kumar", "+94754567890", "321 Beach Road, Negombo"),
                ("Nishma", "Fernando", "+94785678901", "654 Hill Street, Nuwara Eliya"),
                ("Anura", "Jayasinghe", "+94776789012", "987 Market Road, Matara"),
                ("Chandrika", "Gunawardana", "+94718901234", "147 Station Road, Jaffna"),
                ("Mahesh", "Rathnayake", "+94769012345", "258 Port Road, Trincomalee"),
                ("Indira", "Prabodha", "+94770123456", "369 Temple Road, Batticaloa"),
                ("Ashok", "Wijesinghe", "+94762134567", "741 Park Street, Ratnapura")
            };

            var workersToAdd = new List<User>();
            var usersToUpdate = new List<User>();
            int employeeNumber = 2; // EMP001 is used by the default worker.

            foreach (var section in sections.Take(10))
            {
                for (int i = 0; i < 10; i++)
                {
                    var (firstName, lastName, phone, address) = sriLankanWorkers[i];

                    // Generate unique email with section code and sequence number
                    var sectionCode = section.Name.Substring(0, Math.Min(2, section.Name.Length)).ToLower();
                    var email = $"{firstName.ToLower()}.{lastName.ToLower()}.{sectionCode}{i + 1:D2}@slt.lk";
                    var preferredServiceId = BuildEmployeeServiceId(employeeNumber);
                    employeeNumber++;

                    if (existingUsersByEmail.TryGetValue(email.ToLower(), out var existingUser))
                    {
                        var shouldUpdate = false;

                        if (!string.Equals(existingUser.FirstName, firstName, StringComparison.Ordinal))
                        {
                            existingUser.FirstName = firstName;
                            shouldUpdate = true;
                        }

                        if (!string.Equals(existingUser.LastName, lastName, StringComparison.Ordinal))
                        {
                            existingUser.LastName = lastName;
                            shouldUpdate = true;
                        }

                        if (!string.Equals(existingUser.Phone, phone, StringComparison.Ordinal))
                        {
                            existingUser.Phone = phone;
                            shouldUpdate = true;
                        }

                        if (!string.Equals(existingUser.Address, address, StringComparison.Ordinal))
                        {
                            existingUser.Address = address;
                            shouldUpdate = true;
                        }

                        if (existingUser.RoleId != workerRoleId)
                        {
                            existingUser.RoleId = workerRoleId;
                            shouldUpdate = true;
                        }

                        if (existingUser.SectionId != section.Id)
                        {
                            existingUser.SectionId = section.Id;
                            shouldUpdate = true;
                        }

                        if (!existingUser.IsActive)
                        {
                            existingUser.IsActive = true;
                            shouldUpdate = true;
                        }

                        var resolvedServiceId = await GetAvailableServiceIdAsync(
                            preferredServiceId,
                            existingUser.Id);
                        if (!string.Equals(existingUser.ServiceId, resolvedServiceId, StringComparison.Ordinal))
                        {
                            existingUser.ServiceId = resolvedServiceId;
                            shouldUpdate = true;
                        }

                        if (shouldUpdate)
                        {
                            usersToUpdate.Add(existingUser);
                        }

                        continue;
                    }

                    var newWorkerServiceId = await GetAvailableServiceIdAsync(
                        preferredServiceId,
                        null);

                    workersToAdd.Add(new User
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        Email = email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("123"),
                        Phone = phone,
                        Address = address,
                        ServiceId = newWorkerServiceId,
                        RoleId = workerRoleId,
                        SectionId = section.Id,
                        IsActive = true
                    });
                }
            }

            if (workersToAdd.Count > 0)
            {
                context.Users.AddRange(workersToAdd);
            }

            if (workersToAdd.Count > 0 || usersToUpdate.Count > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Seeded {AddedCount} and updated {UpdatedCount} Sri Lankan workers with unique Service IDs.", workersToAdd.Count, usersToUpdate.Count);
            }
            else
            {
                logger.LogInformation("All workers already seeded.");
            }
        }

        private static async Task EnsureDummyAttendanceAsync(
            ApplicationDbContext context,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var workers = await context.Users
                .Where(u => u.Role.Name == RoleNames.Worker)
                .ToListAsync(cancellationToken);

            if (workers.Count == 0)
            {
                logger.LogWarning("No workers found for attendance seeding.");
                return;
            }

            var today = DateTime.Today;
            var thisMonthStart = new DateTime(today.Year, today.Month, 1);
            var lastMonthStart = thisMonthStart.AddMonths(-1);
            var lastMonthEnd = thisMonthStart.AddDays(-1);

            // Last month: keep a reasonable demo range (~10 working days).
            var lastMonthWorkingDays = GetWorkingDays(lastMonthStart, lastMonthEnd);
            var lastMonthDates = lastMonthWorkingDays
                .TakeLast(Math.Min(10, lastMonthWorkingDays.Count))
                .ToList();

            // This month: only from the 1st day up to today, weekdays only.
            var thisMonthDates = GetWorkingDays(thisMonthStart, today);

            var attendanceDates = lastMonthDates
                .Concat(thisMonthDates)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            // Remove any future attendance rows so seeded demo data never contains future dates.
            var futureStartDate = today.AddDays(1);
            var futureAttendances = await context.Attendances
                .Where(a => a.AttendanceDate >= futureStartDate)
                .ToListAsync(cancellationToken);

            if (futureAttendances.Count > 0)
            {
                context.Attendances.RemoveRange(futureAttendances);
            }

            // Simple manually-entered style demo values; no business rule calculations.
            var demoPatterns = new (TimeSpan inTime, TimeSpan outTime, int total, int regular, int overtime, string status, string note)[]
            {
                (new TimeSpan(8, 50, 0), new TimeSpan(17, 05, 0), 495, 480, 15, AttendanceStatus.Present, "Manual demo entry"),
                (new TimeSpan(9, 00, 0), new TimeSpan(17, 30, 0), 510, 480, 30, AttendanceStatus.Present, "Manual demo entry"),
                (new TimeSpan(8, 45, 0), new TimeSpan(17, 20, 0), 515, 480, 35, AttendanceStatus.Present, "Manual demo entry"),
                (new TimeSpan(9, 10, 0), new TimeSpan(17, 25, 0), 495, 450, 45, AttendanceStatus.Late, "Manual demo entry"),
                (new TimeSpan(8, 55, 0), new TimeSpan(17, 15, 0), 500, 480, 20, AttendanceStatus.Present, "Manual demo entry")
            };

            int recordsAdded = 0;

            foreach (var worker in workers)
            {
                foreach (var attendanceDate in attendanceDates)
                {
                    var attendanceExists = await context.Attendances
                        .AnyAsync(
                            a => a.UserId == worker.Id && a.AttendanceDate.Date == attendanceDate.Date,
                            cancellationToken);

                    if (!attendanceExists)
                    {
                        var pattern = demoPatterns[(worker.Id + attendanceDate.Day) % demoPatterns.Length];

                        context.Attendances.Add(new Attendance
                        {
                            UserId = worker.Id,
                            AttendanceDate = attendanceDate.Date,
                            InTime = pattern.inTime,
                            OutTime = pattern.outTime,
                            TotalWorkedMinutes = pattern.total,
                            RegularWorkedMinutes = pattern.regular,
                            OvertimeMinutes = pattern.overtime,
                            Status = pattern.status,
                            Notes = pattern.note,
                            IsLocked = false
                        });
                        recordsAdded++;
                    }
                }
            }

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Seeded {RecordsAdded} dummy attendance records for {DateCount} working days (last month + this month to today), and removed {FutureCount} future records.",
                    recordsAdded,
                    attendanceDates.Count,
                    futureAttendances.Count);
            }
        }

        private static List<DateTime> GetWorkingDays(DateTime startDate, DateTime endDateInclusive)
        {
            var result = new List<DateTime>();

            for (var date = startDate.Date; date <= endDateInclusive.Date; date = date.AddDays(1))
            {
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                result.Add(date);
            }

            return result;
        }

        private static async Task<string> ResolveUniqueServiceIdAsync(
            ApplicationDbContext context,
            string preferredServiceId,
            int? currentUserId,
            CancellationToken cancellationToken)
        {
            var normalizedPreferred = (preferredServiceId ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalizedPreferred))
            {
                normalizedPreferred = BuildEmployeeServiceId(1);
            }

            var preferredUsedByOther = await context.Users
                .AsNoTracking()
                .AnyAsync(
                    u => u.ServiceId != null &&
                         u.ServiceId.ToUpper() == normalizedPreferred &&
                         (!currentUserId.HasValue || u.Id != currentUserId.Value),
                    cancellationToken);

            if (!preferredUsedByOther)
            {
                return normalizedPreferred;
            }

            var startNumber = 1;
            if (normalizedPreferred.StartsWith("EMP", StringComparison.Ordinal) &&
                int.TryParse(normalizedPreferred[3..], out var parsedNumber) &&
                parsedNumber > 0)
            {
                startNumber = parsedNumber + 1;
            }

            return await GetNextAvailableEmployeeServiceIdAsync(context, startNumber, currentUserId, cancellationToken);
        }

        private static async Task<string> GetNextAvailableEmployeeServiceIdAsync(
            ApplicationDbContext context,
            int startNumber,
            int? currentUserId,
            CancellationToken cancellationToken)
        {
            var candidateNumber = Math.Max(1, startNumber);

            while (true)
            {
                var candidate = BuildEmployeeServiceId(candidateNumber);
                var inUse = await context.Users
                    .AsNoTracking()
                    .AnyAsync(
                        u => u.ServiceId != null &&
                             u.ServiceId.ToUpper() == candidate &&
                             (!currentUserId.HasValue || u.Id != currentUserId.Value),
                        cancellationToken);

                if (!inUse)
                {
                    return candidate;
                }

                candidateNumber++;
            }
        }

        private static string BuildEmployeeServiceId(int number)
        {
            return $"EMP{number:D3}";
        }

        private static async Task EnsureNewRoleUsersAsync(
            ApplicationDbContext context,
            ILogger logger,
            int sectionId,
            CancellationToken cancellationToken)
        {
            var roleUsers = new[]
            {
                (RoleNames.GM,       "gm@attendance.com",      "GM@123",       "General",  "Manager",   "EMP910"),
                (RoleNames.DGM,      "dgm@attendance.com",     "DGM@123",      "Deputy",   "Manager",   "EMP911"),
                (RoleNames.Engineer, "engineer@attendance.com","Eng@123",      "Section",  "Engineer",  "EMP912"),
            };

            foreach (var (roleName, email, password, firstName, lastName, preferredServiceId) in roleUsers)
            {
                var roleId = await context.Roles
                    .Where(r => r.Name == roleName)
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (roleId == 0) continue;

                await EnsureUserAsync(
                    context,
                    email: email,
                    password: password,
                    firstName: firstName,
                    lastName: lastName,
                    phone: "0000000000",
                    preferredServiceId: preferredServiceId,
                    roleId: roleId,
                    sectionId: sectionId,
                    cancellationToken);
            }

            logger.LogInformation("GM, DGM, and Engineer seed users ensured.");
        }
    }
}
