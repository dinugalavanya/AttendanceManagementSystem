using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Models;
using AttendanceManagementSystem.Services;
using AttendanceManagementSystem.Data;

namespace AttendanceManagementSystem.Controllers
{
    [Authorize]
    public class AttendanceController : Controller
    {
        private readonly IAttendanceService _attendanceService;
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _context;

        public AttendanceController(IAttendanceService attendanceService, IAuthService authService, ApplicationDbContext context)
        {
            _attendanceService = attendanceService;
            _authService = authService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var todayAttendance = await _attendanceService.GetTodayAttendanceAsync(currentUser.Id);
            var model = new AttendanceViewModel
            {
                TodayAttendance = todayAttendance,
                CanCheckIn = todayAttendance == null,
                CanCheckOut = todayAttendance != null && !todayAttendance.IsLocked,
                CurrentTime = DateTime.Now
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn()
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var attendance = await _attendanceService.CheckInAsync(currentUser.Id);
                TempData["Success"] = "Successfully checked in at " + attendance.InTime?.ToString(@"hh\:mm tt");
                return RedirectToAction("Index");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
            catch
            {
                TempData["Error"] = "An error occurred during check-in. Please try again.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut()
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var attendance = await _attendanceService.CheckOutAsync(currentUser.Id);
                TempData["Success"] = "Successfully checked out at " + attendance.OutTime?.ToString(@"hh\:mm tt");
                return RedirectToAction("Index");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
            catch
            {
                TempData["Error"] = "An error occurred during check-out. Please try again.";
                return RedirectToAction("Index");
            }
        }

        public async Task<IActionResult> History(DateTime? startDate, DateTime? endDate)
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;

            var attendances = await _attendanceService.GetUserAttendancesAsync(currentUser.Id, start, end);

            var model = new AttendanceHistoryViewModel
            {
                Attendances = attendances,
                StartDate = start,
                EndDate = end
            };

            return View(model);
        }

        [Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin}")]
        public async Task<IActionResult> Manage(DateTime? date)
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var selectedDate = date ?? DateTime.Today;

            List<Attendance> attendances;

            if (currentUser.Role.Name == RoleNames.SuperAdmin)
            {
                attendances = await _attendanceService.GetAllAttendancesAsync(selectedDate);
            }
            else // Admin
            {
                if (currentUser.SectionId == null)
                {
                    TempData["Error"] = "You are not assigned to any section.";
                    return RedirectToAction("Index", "Dashboard");
                }

                attendances = await _attendanceService.GetSectionAttendancesAsync(currentUser.SectionId.Value, selectedDate);
            }

            var model = new AttendanceManageViewModel
            {
                Attendances = attendances,
                SelectedDate = selectedDate,
                CanEdit = currentUser.Role.Name == RoleNames.SuperAdmin || currentUser.Role.Name == RoleNames.Admin,
                ScopeLabel = currentUser.Role.Name == RoleNames.SuperAdmin
                    ? "All Sections"
                    : currentUser.Section?.Name ?? "My Section",
                TotalRecords = attendances.Count,
                PresentCount = attendances.Count(a => a.Status == AttendanceStatus.Present),
                LateCount = attendances.Count(a => a.Status == AttendanceStatus.Late),
                AbsentCount = attendances.Count(a => a.Status == AttendanceStatus.Absent),
                OnLeaveCount = attendances.Count(a => a.Status == AttendanceStatus.Leave),
                TotalWorkedHours = Math.Round(attendances.Sum(a => a.TotalWorkedMinutes) / 60m, 1),
                OvertimeHours = Math.Round(attendances.Sum(a => a.OvertimeMinutes) / 60m, 1)
            };

            return View(model);
        }

        [Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin}")]
        public async Task<IActionResult> Edit(int id)
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var attendance = await _attendanceService.GetAttendanceByIdAsync(id);
            if (attendance == null)
            {
                return NotFound();
            }

            // Check if user has permission to edit this attendance
            if (currentUser.Role.Name == RoleNames.Admin)
            {
                if (currentUser.SectionId != attendance.User.SectionId)
                {
                    return Forbid();
                }
            }

            var model = new EditAttendanceViewModel
            {
                Id = attendance.Id,
                UserId = attendance.UserId,
                UserName = attendance.User.FullName,
                AttendanceDate = attendance.AttendanceDate,
                InTime = attendance.InTime,
                OutTime = attendance.OutTime,
                Status = attendance.Status,
                EditReason = string.Empty
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditAttendanceViewModel model)
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var attendance = await _attendanceService.UpdateAttendanceAsync(
                    model.Id, 
                    model.InTime, 
                    model.OutTime, 
                    model.Status, 
                    currentUser.Id, 
                    model.EditReason);

                TempData["Success"] = "Attendance updated successfully.";
                return RedirectToAction("Manage", new { date = model.AttendanceDate });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
            catch
            {
                ModelState.AddModelError("", "An error occurred while updating attendance.");
                return View(model);
            }
        }
    }

    public class AttendanceViewModel
    {
        public Attendance? TodayAttendance { get; set; }
        public bool CanCheckIn { get; set; }
        public bool CanCheckOut { get; set; }
        public DateTime CurrentTime { get; set; }
    }

    public class AttendanceHistoryViewModel
    {
        public List<Attendance> Attendances { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class AttendanceManageViewModel
    {
        public List<Attendance> Attendances { get; set; } = new();
        public DateTime SelectedDate { get; set; }
        public bool CanEdit { get; set; }
        public string ScopeLabel { get; set; } = string.Empty;
        public int TotalRecords { get; set; }
        public int PresentCount { get; set; }
        public int LateCount { get; set; }
        public int AbsentCount { get; set; }
        public int OnLeaveCount { get; set; }
        public decimal TotalWorkedHours { get; set; }
        public decimal OvertimeHours { get; set; }
    }

    public class EditAttendanceViewModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateTime AttendanceDate { get; set; }
        public TimeSpan? InTime { get; set; }
        public TimeSpan? OutTime { get; set; }
        public string Status { get; set; } = AttendanceStatus.Present;
        public string EditReason { get; set; } = string.Empty;
    }
}
