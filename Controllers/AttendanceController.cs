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
        private readonly AttendanceCalculationService _calculationService;

        public AttendanceController(IAttendanceService attendanceService, IAuthService authService, ApplicationDbContext context, AttendanceCalculationService calculationService)
        {
            _attendanceService = attendanceService;
            _authService = authService;
            _context = context;
            _calculationService = calculationService;
        }

        // Helper method to format TimeSpan as "Xh Ym"
        private string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
                duration = TimeSpan.Zero;

            int totalHours = (int)duration.TotalHours;
            int minutes = duration.Minutes;

            return $"{totalHours}h {minutes}m";
        }

        // Helper class for attendance calculation results
        private class AttendanceCalculationResult
        {
            public TimeSpan? TotalWorked { get; set; }
            public TimeSpan? ExpectedOffTime { get; set; }
            public TimeSpan Overtime { get; set; }
            public string StatusText { get; set; } = string.Empty;
            public bool IsIncomplete { get; set; }
        }

        // Centralized helper method to calculate attendance times
        private AttendanceCalculationResult CalculateAttendanceTimes(DateTime attendanceDate, TimeSpan? checkInTime, TimeSpan? checkOutTime)
        {
            var result = new AttendanceCalculationResult();

            if (checkInTime == null)
            {
                result.StatusText = "No Check-in";
                result.TotalWorked = null;
                result.ExpectedOffTime = null;
                result.Overtime = TimeSpan.Zero;
                result.IsIncomplete = true;
                return result;
            }

            var expectedOffTime = checkInTime.Value.Add(new TimeSpan(8, 0, 0));
            result.ExpectedOffTime = expectedOffTime;

            TimeSpan? endTime = null;

            if (checkOutTime != null)
            {
                endTime = checkOutTime.Value;
            }
            else if (attendanceDate.Date == DateTime.Today)
            {
                endTime = DateTime.Now.TimeOfDay;
            }
            else
            {
                result.StatusText = "Incomplete Record";
                result.TotalWorked = null;
                result.Overtime = TimeSpan.Zero;
                result.IsIncomplete = true;
                return result;
            }

            var totalWorked = endTime.Value - checkInTime.Value;
            if (totalWorked < TimeSpan.Zero)
                totalWorked = TimeSpan.Zero;

            var overtime = endTime.Value > expectedOffTime
                ? endTime.Value - expectedOffTime
                : TimeSpan.Zero;

            result.TotalWorked = totalWorked;
            result.Overtime = overtime;
            result.StatusText = checkOutTime != null ? "Checked Out" : "Checked In";

            return result;
        }

        // Safe formatter for duration display
        private string FormatDuration(TimeSpan? duration)
        {
            if (duration == null)
                return "--";

            if (duration.Value < TimeSpan.Zero)
                return "0h 0m";

            return $"{(int)duration.Value.TotalHours}h {duration.Value.Minutes}m";
        }

        // Helper method to format minutes as "Xh Ym" (legacy compatibility)
        private string FormatDuration(int minutes)
        {
            if (minutes <= 0) return "0h 0m";
            var hours = minutes / 60;
            var mins = minutes % 60;
            return $"{hours}h {mins}m";
        }

        public async Task<IActionResult> Index(DateTime? selectedDate)
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var targetDate = selectedDate ?? DateTime.Today;
            var isToday = targetDate.Date == DateTime.Today;
            var currentTime = DateTime.Now;

            // Get attendance for the selected date
            var selectedAttendance = await _context.Attendances
                .Where(a => a.UserId == currentUser.Id && a.AttendanceDate.Date == targetDate.Date)
                .FirstOrDefaultAsync();

            // Calculate OT Duration directly from InTime and OutTime (overtime-only logic)
            int todayOTMinutes = 0;
            if (selectedAttendance != null && selectedAttendance.InTime.HasValue && selectedAttendance.OutTime.HasValue)
            {
                var inDateTime = targetDate.Date + selectedAttendance.InTime.Value;
                var outDateTime = targetDate.Date + selectedAttendance.OutTime.Value;
                
                // Handle case where OT spans midnight (end time is next day)
                if (outDateTime < inDateTime)
                {
                    outDateTime = outDateTime.AddDays(1);
                }
                
                todayOTMinutes = (int)(outDateTime - inDateTime).TotalMinutes;
            }
            
            // Update the database record with correct OT duration if needed
            if (selectedAttendance != null && selectedAttendance.OvertimeMinutes != todayOTMinutes)
            {
                selectedAttendance.OvertimeMinutes = todayOTMinutes;
                selectedAttendance.RegularWorkedMinutes = 0;
                selectedAttendance.TotalWorkedMinutes = todayOTMinutes;
                await _context.SaveChangesAsync();
            }

            // Calculate weekly OT (current week) - recalculate each record with new logic
            var weekStart = targetDate.AddDays(-(int)targetDate.DayOfWeek);
            var weekEnd = weekStart.AddDays(6);
            var weekAttendances = await _context.Attendances
                .Where(a => a.UserId == currentUser.Id &&
                           a.AttendanceDate.Date >= weekStart.Date &&
                           a.AttendanceDate.Date <= weekEnd.Date)
                .ToListAsync();
            
            int weeklyOTMinutes = 0;
            foreach (var att in weekAttendances)
            {
                if (att.InTime.HasValue && att.OutTime.HasValue)
                {
                    var inDateTime = att.AttendanceDate.Date + att.InTime.Value;
                    var outDateTime = att.AttendanceDate.Date + att.OutTime.Value;
                    if (outDateTime < inDateTime)
                    {
                        outDateTime = outDateTime.AddDays(1);
                    }
                    weeklyOTMinutes += (int)(outDateTime - inDateTime).TotalMinutes;
                    
                    // Update record if needed
                    if (att.OvertimeMinutes != (int)(outDateTime - inDateTime).TotalMinutes)
                    {
                        att.OvertimeMinutes = (int)(outDateTime - inDateTime).TotalMinutes;
                        att.RegularWorkedMinutes = 0;
                        att.TotalWorkedMinutes = att.OvertimeMinutes;
                    }
                }
            }
            await _context.SaveChangesAsync();

            // Calculate monthly OT (current month) - recalculate each record with new logic
            var monthStart = new DateTime(targetDate.Year, targetDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var monthAttendances = await _context.Attendances
                .Where(a => a.UserId == currentUser.Id &&
                           a.AttendanceDate.Date >= monthStart.Date &&
                           a.AttendanceDate.Date <= monthEnd.Date)
                .ToListAsync();
            
            int monthlyOTMinutes = 0;
            foreach (var att in monthAttendances)
            {
                if (att.InTime.HasValue && att.OutTime.HasValue)
                {
                    var inDateTime = att.AttendanceDate.Date + att.InTime.Value;
                    var outDateTime = att.AttendanceDate.Date + att.OutTime.Value;
                    if (outDateTime < inDateTime)
                    {
                        outDateTime = outDateTime.AddDays(1);
                    }
                    monthlyOTMinutes += (int)(outDateTime - inDateTime).TotalMinutes;
                    
                    // Update record if needed
                    if (att.OvertimeMinutes != (int)(outDateTime - inDateTime).TotalMinutes)
                    {
                        att.OvertimeMinutes = (int)(outDateTime - inDateTime).TotalMinutes;
                        att.RegularWorkedMinutes = 0;
                        att.TotalWorkedMinutes = att.OvertimeMinutes;
                    }
                }
            }
            await _context.SaveChangesAsync();

            // Format times for display
            string checkInTimeDisplay = "-";
            if (selectedAttendance?.InTime.HasValue == true)
            {
                checkInTimeDisplay = selectedAttendance.InTime.Value.ToString(@"hh\:mm");
            }

            string checkOutTimeDisplay = "-";
            if (selectedAttendance?.OutTime.HasValue == true)
            {
                checkOutTimeDisplay = selectedAttendance.OutTime.Value.ToString(@"hh\:mm");
            }

            string workedTimeDisplay = "-";
            if (selectedAttendance != null && selectedAttendance.TotalWorkedMinutes > 0)
            {
                workedTimeDisplay = FormatDuration(TimeSpan.FromMinutes(selectedAttendance.TotalWorkedMinutes));
            }

            // Format OT displays
            string otDisplay = FormatDuration(TimeSpan.FromMinutes(todayOTMinutes));
            var weeklyOTTime = TimeSpan.FromMinutes(weeklyOTMinutes);
            var monthlyOTTime = TimeSpan.FromMinutes(monthlyOTMinutes);

            var model = new AttendanceViewModel
            {
                TodayAttendance = selectedAttendance,
                SelectedDate = targetDate,
                CurrentTime = currentTime,
                TodayOTHours = todayOTMinutes / 60.0,
                TodayOTDisplay = otDisplay,
                WeeklyOTDisplay = FormatDuration(weeklyOTTime),
                MonthlyOTDisplay = FormatDuration(monthlyOTTime),
                HasOTToday = todayOTMinutes > 0,
                WorkedTimeDisplay = workedTimeDisplay,
                CheckInTimeDisplay = checkInTimeDisplay,
                CheckOutTimeDisplay = checkOutTimeDisplay,
                CurrentStatus = selectedAttendance != null ? "Recorded" : "Not Recorded",
                OvertimeHelperText = todayOTMinutes > 0 ? "Overtime duration entered" : "No overtime recorded",
                RegularWorkMinutes = 0, // No regular work calculation for OT-only page
                TotalWorkMinutes = selectedAttendance?.TotalWorkedMinutes ?? 0,
                IsAlreadySubmitted = selectedAttendance != null
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAttendance(DateTime selectedDate, TimeSpan? inTime, TimeSpan? outTime, string? notes)
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var existingRecord = await _context.Attendances
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a =>
                        a.UserId == currentUser.Id &&
                        a.AttendanceDate.Date == selectedDate.Date);

                // Workers can submit OT once per selected date and cannot edit it afterward from this page.
                if (existingRecord != null)
                {
                    TempData["Error"] = "You have already submitted OT for this date. This record cannot be changed.";
                    return RedirectToAction("Index", new { selectedDate = selectedDate.Date.ToString("yyyy-MM-dd") });
                }

                // Validation: OT Start Time is required
                if (inTime == null)
                {
                    TempData["Error"] = "OT Start Time is required.";
                    return RedirectToAction("Index", new { selectedDate = selectedDate });
                }

                // Validation: OT End Time is required
                if (outTime == null)
                {
                    TempData["Error"] = "OT End Time is required.";
                    return RedirectToAction("Index", new { selectedDate = selectedDate });
                }

                // Create new record (duplicate records are blocked by validation above).
                var existingAttendance = new Attendance
                {
                    UserId = currentUser.Id,
                    AttendanceDate = selectedDate.Date,
                    OtDate = selectedDate.Date,
                    InTime = inTime,
                    OutTime = outTime,
                    Notes = notes,
                    IsLocked = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Attendances.Add(existingAttendance);

                // Calculate OT Duration directly: OT End Time - OT Start Time
                if (inTime.HasValue && outTime.HasValue)
                {
                    var inDateTime = selectedDate.Date + inTime.Value;
                    var outDateTime = selectedDate.Date + outTime.Value;
                    
                    // Handle case where OT spans midnight (end time is next day)
                    if (outDateTime < inDateTime)
                    {
                        outDateTime = outDateTime.AddDays(1);
                    }
                    
                    var otDurationMinutes = (int)(outDateTime - inDateTime).TotalMinutes;
                    
                    // Overtime-only logic: the entire duration is overtime
                    existingAttendance.TotalWorkedMinutes = otDurationMinutes;
                    existingAttendance.RegularWorkedMinutes = 0; // No regular work calculation
                    existingAttendance.OvertimeMinutes = otDurationMinutes; // All time is overtime
                    existingAttendance.Status = AttendanceStatus.Present; // Always present for overtime entries
                }
                else
                {
                    // Should not happen due to validation above, but handle defensively
                    existingAttendance.TotalWorkedMinutes = 0;
                    existingAttendance.RegularWorkedMinutes = 0;
                    existingAttendance.OvertimeMinutes = 0;
                    existingAttendance.Status = AttendanceStatus.Absent;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Overtime saved successfully.";
                return RedirectToAction("Index", new { selectedDate = selectedDate });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SaveAttendance failed: {ex.Message}");
                TempData["Error"] = "An error occurred while saving OT data. Please try again.";
                return RedirectToAction("Index", new { selectedDate = selectedDate });
            }
        }

        public async Task<IActionResult> History(DateTime? selectedDate, DateTime? periodStartDate, DateTime? periodEndDate)
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new AttendanceHistoryViewModel
            {
                // Default period range for backward compatibility
                StartDate = DateTime.Today.AddDays(-30),
                EndDate = DateTime.Today
            };

            // Handle single date selection
            if (selectedDate.HasValue)
            {
                model.SelectedDate = selectedDate.Value;
                model.SelectedDateRecord = await _context.Attendances
                    .Where(a => a.UserId == currentUser.Id && a.AttendanceDate.Date == selectedDate.Value.Date)
                    .FirstOrDefaultAsync();
            }

            // Handle period filtering
            if (periodStartDate.HasValue && periodEndDate.HasValue)
            {
                model.PeriodStartDate = periodStartDate.Value;
                model.PeriodEndDate = periodEndDate.Value;
                model.PeriodRecords = await _attendanceService.GetUserAttendancesAsync(currentUser.Id, periodStartDate.Value, periodEndDate.Value);
            }

            return View(model);
        }

        [Authorize(Roles = RoleNames.EngineerAccessRoles)]
        public async Task<IActionResult> Manage(DateTime? date, int? sectionId)
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var selectedDate = date ?? DateTime.Today;
            var roleName = currentUser.Role.Name;
            var isSuperAdminOrGM = _authService.IsSuperAdminOrGM(currentUser);

            // Check if user has a section assigned (required for non-SuperAdmin/GM)
            if (!isSuperAdminOrGM && currentUser.SectionId == null)
            {
                TempData["Error"] = "You are not assigned to any section.";
                return RedirectToAction("Index", "Dashboard");
            }

            // Use role-based filtering in service layer
            // SuperAdmin/GM: sectionId filters by specific section, null/0 shows all
            // Normal users: always filtered to their assigned section
            List<Attendance> attendances;
            if (isSuperAdminOrGM)
            {
                if (sectionId.HasValue && sectionId > 0)
                {
                    attendances = await _attendanceService.GetSectionAttendancesAsync(sectionId.Value, selectedDate, currentUser);
                }
                else
                {
                    // sectionId == -1 or null means "All Sections"
                    attendances = await _attendanceService.GetAllAttendancesAsync(selectedDate, currentUser);
                }
            }
            else
            {
                // Normal users - filtered to their section by service layer
                attendances = await _attendanceService.GetSectionAttendancesAsync(currentUser.SectionId!.Value, selectedDate, currentUser);
            }

            var canEdit = roleName == RoleNames.SuperAdmin || roleName == RoleNames.Admin || RoleNames.HasEngineerPrivileges(roleName);

            // Build section list for dropdown
            // SuperAdmin and GM see all sections including "All Sections" (Id=-1)
            // Normal users see only their assigned section
            var sections = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
            if (isSuperAdminOrGM)
            {
                // Add "All Sections" option at the top
                sections.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = "-1",
                    Text = "All Sections",
                    Selected = !sectionId.HasValue || sectionId == -1
                });

                var dbSections = await _context.Sections
                    .Where(s => s.IsActive && s.Id != -1) // Exclude "All Sections" from regular list
                    .OrderBy(s => s.Name)
                    .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                    {
                        Value = s.Id.ToString(),
                        Text = s.Name,
                        Selected = sectionId.HasValue && s.Id == sectionId.Value
                    })
                    .ToListAsync();

                sections.AddRange(dbSections);
            }
            else
            {
                // Normal users - show only their assigned section
                if (currentUser.SectionId != null)
                {
                    var userSection = await _context.Sections
                        .Where(s => s.Id == currentUser.SectionId.Value)
                        .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                        {
                            Value = s.Id.ToString(),
                            Text = s.Name,
                            Selected = true
                        })
                        .FirstOrDefaultAsync();
                    
                    if (userSection != null)
                    {
                        sections.Add(userSection);
                    }
                }
            }

            var scopeLabel = isSuperAdminOrGM
                ? (sectionId.HasValue && sectionId > 0
                    ? (sections.FirstOrDefault(s => s.Value == sectionId.ToString())?.Text ?? "Selected Section")
                    : "All Sections")
                : (currentUser.Section?.Name ?? "My Section");

            var model = new AttendanceManageViewModel
            {
                Attendances = attendances,
                Rows = attendances.Select(a => new AttendanceManageRowViewModel
                {
                    AttendanceId = a.Id,
                    EmployeeName = a.User.FullName,
                    EmployeeEmail = a.User.Email,
                    SectionName = a.User.Section?.Name ?? "Unassigned",
                    OvertimeInTime = a.InTime?.ToString(@"hh\:mm") ?? "-",
                    OvertimeOutTime = a.OutTime?.ToString(@"hh\:mm") ?? "-",
                    OvertimeDuration = a.TotalWorkedDisplay,
                    ServiceId = string.IsNullOrWhiteSpace(a.User.ServiceId) ? "-" : a.User.ServiceId,
                    Initials = $"{a.User.FirstName.FirstOrDefault()}{a.User.LastName.FirstOrDefault()}"
                }).ToList(),
                SelectedDate = selectedDate,
                CanEdit = canEdit,
                ScopeLabel = scopeLabel,
                IsGM = isSuperAdminOrGM && roleName == RoleNames.GM,
                SelectedSectionId = sectionId,
                Sections = sections,
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

        [Authorize(Roles = RoleNames.EngineerAccessRoles)]
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
            if (currentUser.Role.Name == RoleNames.Admin || RoleNames.HasEngineerPrivileges(currentUser.Role.Name))
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
        [Authorize(Roles = RoleNames.EngineerAccessRoles)]
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

                TempData["Success"] = "OT record updated successfully.";
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
        [Authorize(Roles = RoleNames.EngineerAccessRoles)]
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
                if (currentUser.Role.Name == RoleNames.Admin || RoleNames.HasEngineerPrivileges(currentUser.Role.Name))
                {
                    if (currentUser.SectionId != attendance.User.SectionId)
                    {
                        Console.WriteLine($"[DEBUG] Permission denied: section mismatch. UserSection={currentUser.SectionId}, AttendanceSection={attendance.User.SectionId}");
                        return Json(new { success = false, message = "You don't have permission to edit this record" });
                    }
                }
                else if (currentUser.Role.Name == RoleNames.DGM || currentUser.Role.Name == RoleNames.GM)
                {
                    // view-only: still allow fetching data for display
                }

                var model = new AttendanceUpdateViewModel
                {
                    Id = attendance.Id,
                    UserId = attendance.UserId,
                    EmployeeName = attendance.User?.FullName ?? "Unknown",
                    OtDate = attendance.OtDate ?? attendance.AttendanceDate,
                    InTime = attendance.InTime,
                    OutTime = attendance.OutTime,
                    Status = attendance.Status,
                    AttendanceDate = attendance.AttendanceDate,
                    WorkedHours = attendance.TotalWorkedMinutes > 0 ? Math.Round(attendance.TotalWorkedMinutes / 60m, 2) : 0,
                    OTHours = attendance.OvertimeMinutes > 0 ? Math.Round(attendance.OvertimeMinutes / 60m, 2) : 0,
                    // Add formatted time strings for HTML input compatibility (24-hour format)
                    InTimeString = attendance.InTime?.ToString(@"hh\:mm"),
                    OutTimeString = attendance.OutTime?.ToString(@"hh\:mm"),
                    OtDateString = (attendance.OtDate ?? attendance.AttendanceDate).ToString("yyyy-MM-dd")
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
                    return Json(new { success = false, message = $"An error occurred while loading OT data: {ex.Message}" });
            }
        }

        // AJAX GET: Get attendance by user and date (used when changing OT Date in modal)
        [HttpGet]
        [Authorize(Roles = RoleNames.EngineerAccessRoles)]
        public async Task<IActionResult> GetAttendanceByUserAndDate(int userId, string date)
        {
            try
            {
                var currentUser = _authService.GetCurrentUser();
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                if (string.IsNullOrWhiteSpace(date))
                {
                    return Json(new { success = false, message = "Date is required" });
                }

                if (!DateTime.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var targetDate))
                {
                    if (!DateTime.TryParse(date, out targetDate))
                    {
                        return Json(new { success = false, message = "Invalid date format" });
                    }
                }

                var attendance = await _context.Attendances
                    .Include(a => a.User)
                    .FirstOrDefaultAsync(a => a.UserId == userId && a.AttendanceDate.Date == targetDate.Date);

                if (attendance == null)
                {
                    return Json(new { success = false, message = "No attendance found for selected date" });
                }

                // Permission check for Admins and Engineers
                if ((currentUser.Role.Name == RoleNames.Admin || RoleNames.HasEngineerPrivileges(currentUser.Role.Name)) && currentUser.SectionId != attendance.User.SectionId)
                {
                    return Json(new { success = false, message = "You don't have permission to view this record" });
                }

                var data = new
                {
                    inTimeString = attendance.InTime?.ToString(@"hh\:mm"),
                    outTimeString = attendance.OutTime?.ToString(@"hh\:mm"),
                    status = attendance.Status
                };

                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error in GetAttendanceByUserAndDate: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while loading attendance" });
            }
        }

        // AJAX POST: Update attendance record
        [HttpPost]
        [Authorize(Roles = RoleNames.EngineerAccessRoles)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAttendance([FromBody] AttendanceUpdateDTO dto)
        {
            try
            {
                Console.WriteLine($"[DEBUG] UpdateAttendance called with: Id={dto?.Id}, OtDate={dto?.OtDate}, InTime={dto?.InTime}, OutTime={dto?.OutTime}, Status={dto?.Status}");

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
                    return Json(new { success = false, message = "OT In Time is required" });
                }

                if (string.IsNullOrWhiteSpace(dto.OutTime))
                {
                    Console.WriteLine("[DEBUG] OutTime is null or empty");
                    return Json(new { success = false, message = "OT Out Time is required" });
                }

                if (string.IsNullOrWhiteSpace(dto.OtDate))
                {
                    Console.WriteLine("[DEBUG] OtDate is null or empty");
                    return Json(new { success = false, message = "OT Date is required" });
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

                // OT-first stage: keep Status internal and default to Present.
                var normalizedStatus = AttendanceStatus.Present;

                // Parse time strings "hh:mm" to TimeSpan with fallback
                TimeSpan inTimeSpan, outTimeSpan;
                
                if (!TimeSpan.TryParseExact(dto.InTime, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out inTimeSpan))
                {
                    if (!TimeSpan.TryParse(dto.InTime, System.Globalization.CultureInfo.InvariantCulture, out inTimeSpan))
                    {
                        Console.WriteLine($"[DEBUG] Failed to parse InTime: {dto.InTime}");
                        return Json(new { success = false, message = "Invalid OT In Time format. Use HH:mm format (e.g., 17:00)" });
                    }
                }

                if (!TimeSpan.TryParseExact(dto.OutTime, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out outTimeSpan))
                {
                    if (!TimeSpan.TryParse(dto.OutTime, System.Globalization.CultureInfo.InvariantCulture, out outTimeSpan))
                    {
                        Console.WriteLine($"[DEBUG] Failed to parse OutTime: {dto.OutTime}");
                        return Json(new { success = false, message = "Invalid OT Out Time format. Use HH:mm format (e.g., 19:30)" });
                    }
                }

                if (!DateTime.TryParseExact(dto.OtDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var otDate))
                {
                    Console.WriteLine($"[DEBUG] Failed to parse OtDate: {dto.OtDate}");
                    return Json(new { success = false, message = "Invalid OT Date format. Use yyyy-MM-dd format." });
                }

                Console.WriteLine($"[DEBUG] Parsed values: OtDate={otDate:yyyy-MM-dd}, InTime={inTimeSpan}, OutTime={outTimeSpan}");

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
                if (currentUser.Role.Name == RoleNames.Admin || RoleNames.HasEngineerPrivileges(currentUser.Role.Name))
                {
                    if (currentUser.SectionId != attendance.User.SectionId)
                    {
                        Console.WriteLine($"[DEBUG] Permission denied: section mismatch.");
                        return Json(new { success = false, message = "You don't have permission to edit this record" });
                    }
                }

                Console.WriteLine($"[DEBUG] Updating attendance with: InTime={inTimeSpan}, OutTime={outTimeSpan}, Status={normalizedStatus}");

                // Update attendance fields
                attendance.OtDate = otDate.Date;
                attendance.InTime = inTimeSpan;
                attendance.OutTime = outTimeSpan;
                attendance.Status = normalizedStatus;
                attendance.UpdatedAt = DateTime.UtcNow;

                // OT duration logic: if OT Out is earlier than OT In, treat OT Out as next day.
                var inDateTime = attendance.AttendanceDate.Date + inTimeSpan;
                var outDateTime = attendance.AttendanceDate.Date + outTimeSpan;
                if (outDateTime < inDateTime)
                {
                    outDateTime = outDateTime.AddDays(1);
                }

                var totalWorkedMinutes = Math.Max(0, (int)(outDateTime - inDateTime).TotalMinutes);
                attendance.TotalWorkedMinutes = totalWorkedMinutes;
                attendance.RegularWorkedMinutes = 0;
                attendance.OvertimeMinutes = totalWorkedMinutes;

                // Save changes
                await _context.SaveChangesAsync();

                Console.WriteLine($"[DEBUG] Successfully updated attendance: {attendance.Id}");

                // Return updated data for table refresh
                var responseData = new
                {
                    id = attendance.Id,
                    otDate = attendance.OtDate?.ToString("yyyy-MM-dd"),
                    inTime = attendance.InTime?.ToString(@"hh\:mm"),
                    outTime = attendance.OutTime?.ToString(@"hh\:mm"),
                    workedHours = Math.Round(attendance.TotalWorkedMinutes / 60m, 2),
                    otHours = Math.Round(attendance.OvertimeMinutes / 60m, 2),
                    status = attendance.Status
                };

                return Json(new { success = true, message = "OT record updated successfully", data = responseData });
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
}
