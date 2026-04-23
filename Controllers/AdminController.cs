using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.Models;
using AttendanceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceManagementSystem.Controllers
{
    [Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin}")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var totalUsers = await _context.Users.AsNoTracking().CountAsync();
            var totalSections = await _context.Sections.AsNoTracking().CountAsync(s => s.IsActive);

            var attendanceByStatus = await _context.Attendances
                .AsNoTracking()
                .Where(a => a.AttendanceDate >= today && a.AttendanceDate < tomorrow)
                .GroupBy(a => a.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var model = new AdminDashboardViewModel
            {
                TotalUsers = totalUsers,
                TotalSections = totalSections,
                TodayAttendance = attendanceByStatus.Sum(x => x.Count),
                PresentCount = attendanceByStatus.FirstOrDefault(x => x.Status == AttendanceStatus.Present)?.Count ?? 0,
                LateCount = attendanceByStatus.FirstOrDefault(x => x.Status == AttendanceStatus.Late)?.Count ?? 0,
                AbsentCount = attendanceByStatus.FirstOrDefault(x => x.Status == AttendanceStatus.Absent)?.Count ?? 0
            };

            return View(model);
        }
    }
}
