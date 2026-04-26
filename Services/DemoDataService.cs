using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Services
{
    public class DemoDataService
    {
        private readonly ApplicationDbContext _context;
        private readonly Random _random = new Random();

        public DemoDataService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SeedDemoDataAsync()
        {
            // Check if demo data already exists
            if (await _context.Users.CountAsync() > 5)
            {
                return; // Demo data already exists
            }

            await SeedDemoUsersAsync();
            await SeedDemoAttendancesAsync();
        }

        private async Task SeedDemoUsersAsync()
        {
            var demoUsers = new List<User>
            {
                new User
                {
                    FirstName = "John",
                    LastName = "Smith",
                    Email = "john.smith@company.com",
                    PasswordHash = "AQAAAAEAACcQAAAAEKqgk8tJ8X7w9v9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X",
                    Phone = "+1-555-0101",
                    Address = "123 Main St, New York, NY 10001",
                    IsActive = true,
                    RoleId = 3, // Worker
                    SectionId = 1
                },
                new User
                {
                    FirstName = "Sarah",
                    LastName = "Johnson",
                    Email = "sarah.johnson@company.com",
                    PasswordHash = "AQAAAAEAACcQAAAAEKqgk8tJ8X7w9v9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X",
                    Phone = "+1-555-0102",
                    Address = "456 Oak Ave, New York, NY 10002",
                    IsActive = true,
                    RoleId = 3, // Worker
                    SectionId = 1
                },
                new User
                {
                    FirstName = "Michael",
                    LastName = "Chen",
                    Email = "michael.chen@company.com",
                    PasswordHash = "AQAAAAEAACcQAAAAEKqgk8tJ8X7w9v9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X",
                    Phone = "+1-555-0103",
                    Address = "789 Pine Rd, New York, NY 10003",
                    IsActive = true,
                    RoleId = 3, // Worker
                    SectionId = 2
                },
                new User
                {
                    FirstName = "Emily",
                    LastName = "Davis",
                    Email = "emily.davis@company.com",
                    PasswordHash = "AQAAAAEAACcQAAAAEKqgk8tJ8X7w9v9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X",
                    Phone = "+1-555-0104",
                    Address = "321 Elm St, New York, NY 10004",
                    IsActive = true,
                    RoleId = 3, // Worker
                    SectionId = 2
                },
                new User
                {
                    FirstName = "Robert",
                    LastName = "Wilson",
                    Email = "robert.wilson@company.com",
                    PasswordHash = "AQAAAAEAACcQAAAAEKqgk8tJ8X7w9v9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X",
                    Phone = "+1-555-0105",
                    Address = "654 Maple Dr, New York, NY 10005",
                    IsActive = true,
                    RoleId = 3, // Worker
                    SectionId = 3
                },
                new User
                {
                    FirstName = "Lisa",
                    LastName = "Anderson",
                    Email = "lisa.anderson@company.com",
                    PasswordHash = "AQAAAAEAACcQAAAAEKqgk8tJ8X7w9v9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X",
                    Phone = "+1-555-0106",
                    Address = "987 Cedar Ln, New York, NY 10006",
                    IsActive = true,
                    RoleId = 3, // Worker
                    SectionId = 3
                },
                new User
                {
                    FirstName = "David",
                    LastName = "Taylor",
                    Email = "david.taylor@company.com",
                    PasswordHash = "AQAAAAEAACcQAAAAEKqgk8tJ8X7w9v9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X",
                    Phone = "+1-555-0107",
                    Address = "246 Birch Way, New York, NY 10007",
                    IsActive = true,
                    RoleId = 3, // Worker
                    SectionId = 1
                },
                new User
                {
                    FirstName = "Jennifer",
                    LastName = "Brown",
                    Email = "jennifer.brown@company.com",
                    PasswordHash = "AQAAAAEAACcQAAAAEKqgk8tJ8X7w9v9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X",
                    Phone = "+1-555-0108",
                    Address = "135 Spruce St, New York, NY 10008",
                    IsActive = true,
                    RoleId = 3, // Worker
                    SectionId = 2
                },
                new User
                {
                    FirstName = "James",
                    LastName = "Martinez",
                    Email = "james.martinez@company.com",
                    PasswordHash = "AQAAAAEAACcQAAAAEKqgk8tJ8X7w9v9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X",
                    Phone = "+1-555-0109",
                    Address = "864 Willow Ave, New York, NY 10009",
                    IsActive = true,
                    RoleId = 3, // Worker
                    SectionId = 3
                },
                new User
                {
                    FirstName = "Amanda",
                    LastName = "Garcia",
                    Email = "amanda.garcia@company.com",
                    PasswordHash = "AQAAAAEAACcQAAAAEKqgk8tJ8X7w9v9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X9X",
                    Phone = "+1-555-0110",
                    Address = "573 Poplar Dr, New York, NY 10010",
                    IsActive = true,
                    RoleId = 3, // Worker
                    SectionId = 1
                }
            };

            await _context.Users.AddRangeAsync(demoUsers);
            await _context.SaveChangesAsync();
        }

        private async Task SeedDemoAttendancesAsync()
        {
            var users = await _context.Users.Where(u => u.RoleId == 3).ToListAsync();
            var attendances = new List<Attendance>();

            // Generate attendance for the last 30 days
            for (int dayOffset = 29; dayOffset >= 0; dayOffset--)
            {
                var date = DateTime.Today.AddDays(-dayOffset);
                
                // Skip weekends (Saturday and Sunday)
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                foreach (var user in users)
                {
                    // 85% chance of being present, 10% late, 5% absent
                    var rand = _random.Next(100);
                    
                    if (rand < 85) // Present
                    {
                        var attendance = GenerateAttendance(user, date, AttendanceStatus.Present);
                        attendances.Add(attendance);
                    }
                    else if (rand < 95) // Late
                    {
                        var attendance = GenerateAttendance(user, date, AttendanceStatus.Late);
                        attendances.Add(attendance);
                    }
                    // Absent - don't create attendance record
                }
            }

            await _context.Attendances.AddRangeAsync(attendances);
            await _context.SaveChangesAsync();
        }

        private Attendance GenerateAttendance(User user, DateTime date, string status)
        {
            var attendance = new Attendance
            {
                UserId = user.Id,
                AttendanceDate = date,
                Status = status,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            if (status == AttendanceStatus.Present)
            {
                // Present: Check in between 8:15 AM and 8:45 AM
                var checkInHour = 8;
                var checkInMinute = _random.Next(15, 46);
                attendance.InTime = new TimeSpan(checkInHour, checkInMinute, _random.Next(0, 60));

                // Check out between 4:30 PM and 6:00 PM
                var checkOutHour = _random.Next(16, 19);
                var checkOutMinute = _random.Next(0, 60);
                attendance.OutTime = new TimeSpan(checkOutHour, checkOutMinute, _random.Next(0, 60));

                attendance.IsLocked = true;
            }
            else if (status == AttendanceStatus.Late)
            {
                // Late: Check in between 8:46 AM and 9:30 AM
                var checkInHour = 8;
                var checkInMinute = _random.Next(46, 60);
                if (checkInMinute == 60)
                {
                    checkInHour = 9;
                    checkInMinute = _random.Next(0, 31);
                }
                attendance.InTime = new TimeSpan(checkInHour, checkInMinute, _random.Next(0, 60));

                // Check out between 4:30 PM and 6:00 PM
                var checkOutHour = _random.Next(16, 19);
                var checkOutMinute = _random.Next(0, 60);
                attendance.OutTime = new TimeSpan(checkOutHour, checkOutMinute, _random.Next(0, 60));

                attendance.IsLocked = true;
            }

            // Calculate work hours if both times are set
            if (attendance.InTime.HasValue && attendance.OutTime.HasValue)
            {
                CalculateWorkHours(attendance);
            }

            return attendance;
        }

        private void CalculateWorkHours(Attendance attendance)
        {
            var workStart = WorkingHours.WorkStart;
            var workEnd = WorkingHours.WorkEnd;

            var actualStart = attendance.InTime.Value < workStart ? workStart : attendance.InTime.Value;
            var actualEnd = attendance.OutTime.Value > workEnd ? workEnd : attendance.OutTime.Value;

            if (actualEnd > actualStart)
            {
                attendance.RegularWorkedMinutes = (int)(actualEnd - actualStart).TotalMinutes;
            }
            else
            {
                attendance.RegularWorkedMinutes = 0;
            }

            // Calculate overtime (time after 4:30 PM)
            if (attendance.OutTime.HasValue && attendance.OutTime.Value > workEnd)
            {
                attendance.OvertimeMinutes = (int)(attendance.OutTime.Value - workEnd).TotalMinutes;
            }
            else
            {
                attendance.OvertimeMinutes = 0;
            }

            // Calculate total worked minutes
            attendance.TotalWorkedMinutes = attendance.RegularWorkedMinutes + attendance.OvertimeMinutes;
        }
    }
}
