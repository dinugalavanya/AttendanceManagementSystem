using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Models;
using AttendanceManagementSystem.Services;
using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.ViewModels;
using AttendanceManagementSystem.DTOs;

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

        // AJAX GET: Get attendance data for edit modal
        [HttpGet]
        [Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin}")]
        public async Task<IActionResult> GetAttendanceForEdit(int id)
        {
            try
            {
                Console.WriteLine($"[DEBUG] GetAttendanceForEdit called with id: {id}");

                var currentUser = _authService.GetCurrentUser();
                if (currentUser == null)
                {
                    Console.WriteLine("[DEBUG] Current user is null");
                    return Json(new { success = false, message = "User not authenticated" });
                }

                if (currentUser.Role == null)
                {
                    Console.WriteLine("[DEBUG] Current user role is null");
                    return Json(new { success = false, message = "User role not found" });
                }

                Console.WriteLine($"[DEBUG] Current user: {currentUser.FullName}, Role: {currentUser.Role.Name}");

                // Load attendance with User and Section navigation properties
                var attendance = await _context.Attendances
                    .Include(a => a.User)
                    .ThenInclude(u => u.Section)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (attendance == null)
                {
                    Console.WriteLine($"[DEBUG] Attendance not found for id: {id}");
                    return Json(new { success = false, message = "Attendance record not found" });
                }

                if (attendance.User == null)
                {
                    Console.WriteLine($"[DEBUG] Attendance.User is null for attendance id: {id}");
                    return Json(new { success = false, message = "User information not found for attendance record" });
                }

                Console.WriteLine($"[DEBUG] Found attendance: UserId={attendance.UserId}, UserName={attendance.User.FullName}, Section={attendance.User.Section?.Name}");

                // Check if user has permission to edit this attendance
                if (currentUser.Role.Name == RoleNames.Admin)
                {
                    if (currentUser.SectionId != attendance.User.SectionId)
                    {
                        Console.WriteLine($"[DEBUG] Permission denied: Admin section mismatch. UserSection={currentUser.SectionId}, AttendanceSection={attendance.User.SectionId}");
                        return Json(new { success = false, message = "You don't have permission to edit this record" });
                    }
                }

                var model = new AttendanceUpdateViewModel
                {
                    Id = attendance.Id,
                    UserId = attendance.UserId,
                    EmployeeName = attendance.User?.FullName ?? "Unknown",
                    InTime = attendance.InTime,
                    OutTime = attendance.OutTime,
                    Status = attendance.Status,
                    AttendanceDate = attendance.AttendanceDate,
                    WorkedHours = attendance.TotalWorkedMinutes > 0 ? Math.Round(attendance.TotalWorkedMinutes / 60m, 2) : 0,
                    OTHours = attendance.OvertimeMinutes > 0 ? Math.Round(attendance.OvertimeMinutes / 60m, 2) : 0,
                    // Add formatted time strings for HTML input compatibility (24-hour format)
                    InTimeString = attendance.InTime?.ToString(@"hh\:mm"),
                    OutTimeString = attendance.OutTime?.ToString(@"hh\:mm")
                };

                Console.WriteLine($"[DEBUG] Returning model with InTimeString: {model.InTimeString}, OutTimeString: {model.OutTimeString}");

                return Json(new { success = true, data = model });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error in GetAttendanceForEdit: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[DEBUG] Inner Exception: {ex.InnerException.Message}");
                }
                return Json(new { success = false, message = $"An error occurred while loading attendance data: {ex.Message}" });
            }
        }

        // AJAX POST: Update attendance record
        [HttpPost]
        [Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAttendance([FromBody] AttendanceUpdateDTO dto)
        {
            try
            {
                Console.WriteLine($"[DEBUG] UpdateAttendance called with: Id={dto?.Id}, InTime={dto?.InTime}, OutTime={dto?.OutTime}, Status={dto?.Status}");

                // Log ModelState errors if any
                if (!ModelState.IsValid)
                {
                    Console.WriteLine("[DEBUG] ModelState is invalid:");
                    foreach (var state in ModelState)
                    {
                        foreach (var error in state.Value.Errors)
                        {
                            Console.WriteLine($"[DEBUG] {state.Key}: {error.ErrorMessage}");
                        }
                    }
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return Json(new { success = false, message = "Validation failed", errors = errors });
                }

                // Manual validation
                if (dto == null)
                {
                    Console.WriteLine("[DEBUG] DTO is null");
                    return Json(new { success = false, message = "Invalid request data" });
                }

                if (dto.Id <= 0)
                {
                    Console.WriteLine($"[DEBUG] Invalid Id: {dto.Id}");
                    return Json(new { success = false, message = "Invalid attendance record ID" });
                }

                if (string.IsNullOrWhiteSpace(dto.InTime))
                {
                    Console.WriteLine("[DEBUG] InTime is null or empty");
                    return Json(new { success = false, message = "In Time is required" });
                }

                if (string.IsNullOrWhiteSpace(dto.OutTime))
                {
                    Console.WriteLine("[DEBUG] OutTime is null or empty");
                    return Json(new { success = false, message = "Out Time is required" });
                }

                if (string.IsNullOrWhiteSpace(dto.Status))
                {
                    Console.WriteLine("[DEBUG] Status is null or empty");
                    return Json(new { success = false, message = "Status is required" });
                }

                var currentUser = _authService.GetCurrentUser();
                if (currentUser == null)
                {
                    Console.WriteLine("[DEBUG] User not authenticated");
                    return Json(new { success = false, message = "User not authenticated" });
                }

                // Check current user role
                if (currentUser.Role == null)
                {
                    Console.WriteLine("[DEBUG] Current user role is null");
                    return Json(new { success = false, message = "User role not found" });
                }

                Console.WriteLine($"[DEBUG] Current user: {currentUser.FullName}, Role: {currentUser.Role.Name}");

                // Validate and normalize status
                var normalizedStatus = dto.Status;
                if (dto.Status == "On Time")
                {
                    normalizedStatus = AttendanceStatus.Present;
                    Console.WriteLine($"[DEBUG] Converting 'On Time' to 'Present'");
                }

                // Validate status against allowed values
                var validStatuses = new[] { AttendanceStatus.Present, AttendanceStatus.Late, AttendanceStatus.Absent, AttendanceStatus.Leave };
                if (!validStatuses.Contains(normalizedStatus))
                {
                    Console.WriteLine($"[DEBUG] Invalid status: {normalizedStatus}");
                    return Json(new { success = false, message = "Invalid status value" });
                }

                // Parse time strings "hh:mm" to TimeSpan with fallback
                TimeSpan inTimeSpan, outTimeSpan;
                
                if (!TimeSpan.TryParseExact(dto.InTime, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out inTimeSpan))
                {
                    if (!TimeSpan.TryParse(dto.InTime, System.Globalization.CultureInfo.InvariantCulture, out inTimeSpan))
                    {
                        Console.WriteLine($"[DEBUG] Failed to parse InTime: {dto.InTime}");
                        return Json(new { success = false, message = "Invalid In Time format. Use HH:mm format (e.g., 09:30)" });
                    }
                }

                if (!TimeSpan.TryParseExact(dto.OutTime, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out outTimeSpan))
                {
                    if (!TimeSpan.TryParse(dto.OutTime, System.Globalization.CultureInfo.InvariantCulture, out outTimeSpan))
                    {
                        Console.WriteLine($"[DEBUG] Failed to parse OutTime: {dto.OutTime}");
                        return Json(new { success = false, message = "Invalid Out Time format. Use HH:mm format (e.g., 17:30)" });
                    }
                }

                Console.WriteLine($"[DEBUG] Parsed times: InTime={inTimeSpan}, OutTime={outTimeSpan}");

                // Validate time logic
                if (inTimeSpan >= outTimeSpan)
                {
                    Console.WriteLine($"[DEBUG] Time validation failed: {inTimeSpan} >= {outTimeSpan}");
                    return Json(new { success = false, message = "Out Time must be after In Time" });
                }

                // Load attendance with User and Section navigation properties using Id only
                var attendance = await _context.Attendances
                    .Include(a => a.User)
                    .ThenInclude(u => u.Section)
                    .FirstOrDefaultAsync(a => a.Id == dto.Id);

                if (attendance == null)
                {
                    Console.WriteLine($"[DEBUG] Attendance record not found for Id: {dto.Id}");
                    return Json(new { success = false, message = "Attendance record not found" });
                }

                if (attendance.User == null)
                {
                    Console.WriteLine("[DEBUG] Attendance.User is null");
                    return Json(new { success = false, message = "User information not found for attendance record" });
                }

                Console.WriteLine($"[DEBUG] Found attendance: UserId={attendance.UserId}, UserName={attendance.User.FullName}, Section={attendance.User.Section?.Name}");

                // Check permissions
                if (currentUser.Role.Name == RoleNames.Admin)
                {
                    if (currentUser.SectionId != attendance.User.SectionId)
                    {
                        Console.WriteLine($"[DEBUG] Permission denied: Admin section mismatch. UserSection={currentUser.SectionId}, AttendanceSection={attendance.User.SectionId}");
                        return Json(new { success = false, message = "You don't have permission to edit this record" });
                    }
                }

                Console.WriteLine($"[DEBUG] Updating attendance with: InTime={inTimeSpan}, OutTime={outTimeSpan}, Status={normalizedStatus}");

                // Update attendance fields
                attendance.InTime = inTimeSpan;
                attendance.OutTime = outTimeSpan;
                attendance.Status = normalizedStatus;
                attendance.UpdatedAt = DateTime.UtcNow;

                // Calculate worked minutes
                var totalWorkedMinutes = (int)(outTimeSpan - inTimeSpan).TotalMinutes;
                attendance.TotalWorkedMinutes = totalWorkedMinutes;

                // Calculate regular and overtime minutes (8 hours = 480 minutes)
                var regularWorkMinutes = Math.Min(totalWorkedMinutes, 480);
                var overtimeMinutes = Math.Max(0, totalWorkedMinutes - 480);

                attendance.RegularWorkedMinutes = regularWorkMinutes;
                attendance.OvertimeMinutes = overtimeMinutes;

                // Save changes
                await _context.SaveChangesAsync();

                Console.WriteLine($"[DEBUG] Successfully updated attendance: {attendance.Id}");

                // Return updated data for table refresh
                var responseData = new
                {
                    id = attendance.Id,
                    inTime = attendance.InTime?.ToString(@"hh\:mm"),
                    outTime = attendance.OutTime?.ToString(@"hh\:mm"),
                    workedHours = Math.Round(attendance.TotalWorkedMinutes / 60m, 2),
                    otHours = Math.Round(attendance.OvertimeMinutes / 60m, 2),
                    status = attendance.Status
                };

                return Json(new { success = true, message = "Attendance updated successfully", data = responseData });
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[DEBUG] InvalidOperationException: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack Trace: {ex.StackTrace}");
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Exception: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[DEBUG] Inner Exception: {ex.InnerException.Message}");
                }
                return Json(new { success = false, message = $"An error occurred while updating attendance: {ex.Message}" });
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
