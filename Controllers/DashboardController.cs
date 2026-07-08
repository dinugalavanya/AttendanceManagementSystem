using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Models;
using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.ViewModels;
using AttendanceManagementSystem.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using static BCrypt.Net.BCrypt;

namespace AttendanceManagementSystem.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AttendanceCalculationService _calculationService;
        private readonly ILogger<DashboardController> _logger;
        private readonly IEmailService _emailService;
        private readonly IAuthService _authService;

        public DashboardController(ApplicationDbContext context, AttendanceCalculationService calculationService, ILogger<DashboardController> logger, IEmailService emailService, IAuthService authService)
        {
            _context = context;
            _calculationService = calculationService;
            _logger = logger;
            _emailService = emailService;
            _authService = authService;
        }

        public async Task<IActionResult> Index(string? serviceId, DateTime? selectedDate, DateTime? singleDate, DateTime? fromDate, DateTime? toDate)
        {
            _logger.LogDebug("[WORKER SEARCH] serviceId={ServiceId}, selectedDate={SelectedDate}, singleDate={SingleDate}, fromDate={FromDate}, toDate={ToDate}",
                serviceId,
                selectedDate?.ToString("yyyy-MM-dd"),
                singleDate?.ToString("yyyy-MM-dd"),
                fromDate?.ToString("yyyy-MM-dd"),
                toDate?.ToString("yyyy-MM-dd"));

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var currentUser = await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Section)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Use selectedDate if provided, otherwise use today (admin fallback can override this later)
            var today = selectedDate?.Date ?? DateTime.Today;
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEndExclusive = monthStart.AddMonths(1);

            var roleName = currentUser.Role.Name;
            var isSuperAdmin = roleName == "SuperAdmin" || roleName == "superadmin" || roleName == "Super Admin" || roleName == "Super admin";
            var isAdmin = roleName == "Admin" || roleName == "admin";
            var isGM = roleName == "GM" || roleName == "gm";
            var isDGM = roleName == "DGM" || roleName == "dgm";
            var isEngineer = RoleNames.HasEngineerPrivileges(roleName);
            var isWorker = roleName == "Worker" || roleName == "worker";
            var isLeaveAgent = roleName == "Leave Agent" || roleName == "Leave agent" || roleName == "LeaveAgent";

            var userScope = _context.Users.AsNoTracking().Where(u => u.IsActive);
            var attendanceScope = _context.Attendances.AsNoTracking().AsQueryable();

            if (isWorker)
            {
                userScope = userScope.Where(u => u.Id == currentUser.Id);
                attendanceScope = attendanceScope.Where(a => a.UserId == currentUser.Id);
            }
            else if (isLeaveAgent)
            {
                if (currentUser.SectionId.HasValue && currentUser.SectionId.Value > 0)
                {
                    userScope = userScope.Where(u => u.SectionId == currentUser.SectionId.Value);
                    attendanceScope = attendanceScope.Where(a => a.User.SectionId == currentUser.SectionId.Value);
                }
            }
            else if (isAdmin || isEngineer)
            {
                if (!SectionIds.IsAssignable(currentUser.SectionId))
                {
                    return View("AdminDashboard", new AdminDashboardViewModel
                    {
                        HasSection = false,
                        NoSectionMessage = "No valid section assigned to this account. Section 0 is reserved for SuperAdmin and GM only."
                    });
                }

                var searchRequest = new WorkerOtSearchRequestViewModel
                {
                    ServiceId = serviceId,
                    SelectedDate = selectedDate,
                    SingleDate = singleDate,
                    FromDate = fromDate,
                    ToDate = toDate
                };

                var adminDashboardViewModel = await BuildAdminWorkerSearchViewModelAsync(currentUser, searchRequest);
                adminDashboardViewModel.IsReadOnly = isEngineer; // Engineer: view+edit own section; handled in controller
                return View("AdminDashboard", adminDashboardViewModel);
            }
            else if (isDGM)
            {
                if (!SectionIds.IsAssignable(currentUser.SectionId))
                {
                    return View("AdminDashboard", new AdminDashboardViewModel { HasSection = false });
                }

                var searchRequest = new WorkerOtSearchRequestViewModel
                {
                    ServiceId = serviceId,
                    SelectedDate = selectedDate,
                    SingleDate = singleDate,
                    FromDate = fromDate,
                    ToDate = toDate
                };

                var dgmViewModel = await BuildAdminWorkerSearchViewModelAsync(currentUser, searchRequest);
                dgmViewModel.IsReadOnly = true;
                return View("AdminDashboard", dgmViewModel);
            }
            else if (isGM)
            {
                // GM sees all sections — reuse the SuperAdmin-style Index view
                // No early return; falls through to the full dashboard build below
            }

            if (isWorker)
            {
                var searchDate = selectedDate?.Date ?? DateTime.Today;
                var monthStartDate = new DateTime(searchDate.Year, searchDate.Month, 1);
                var rangeEndExclusive = searchDate.AddDays(1);

                // Worker scope must always be month-start through selected date for the logged-in worker.
                var workerRangeRecords = await _context.Attendances
                    .AsNoTracking()
                    .Where(a =>
                        a.UserId == currentUser.Id &&
                        a.AttendanceDate >= monthStartDate &&
                        a.AttendanceDate < rangeEndExclusive)
                    .OrderByDescending(a => a.AttendanceDate)
                    .ThenByDescending(a => a.OutTime)
                    .ToListAsync();

                var workerOtDashboard = BuildWorkerOtDashboardViewModel(currentUser, monthStartDate, searchDate, workerRangeRecords);

                var workerViewModel = new DashboardViewModel
                {
                    SelectedDate = searchDate,
                    ScopeTitle = "My OT overview",
                    IsSuperAdmin = false,
                    IsAdmin = false,
                    IsWorker = true,
                    OvertimeMinutesMonth = workerOtDashboard.TotalOtMinutes,
                    UserName = User.Identity?.Name ?? currentUser.FullName,
                    WorkerOtDashboard = workerOtDashboard
                };

                return View("WorkerDashboard", workerViewModel);
            }

            var totalUsers = await userScope
                .AsNoTracking()
                .CountAsync();

            var todayAttendanceStats = attendanceScope
                .Where(a => a.AttendanceDate >= today && a.AttendanceDate < tomorrow);
                
            var statusCounts = await todayAttendanceStats
                .GroupBy(a => a.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var monthlyStatusCounts = await attendanceScope
                .Where(a => a.AttendanceDate >= monthStart && a.AttendanceDate < monthEndExclusive)
                .GroupBy(a => a.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var sectionSnapshots = new List<SectionSnapshotItem>();
            if (!isWorker)
            {
                sectionSnapshots = await todayAttendanceStats
                    .Where(a => a.User.Section != null)
                    .GroupBy(a => a.User.Section!.Name)
                    .Select(g => new SectionSnapshotItem
                    {
                        SectionName = g.Key,
                        PresentCount = g.Count(a => a.Status == AttendanceStatus.Present),
                        LateCount = g.Count(a => a.Status == AttendanceStatus.Late),
                        AbsentCount = g.Count(a => a.Status == AttendanceStatus.Absent)
                    })
                    .OrderByDescending(x => x.PresentCount + x.LateCount + x.AbsentCount)
                    .Take(6)
                    .ToListAsync();
            }

            var recentAttendance = await attendanceScope
                .Where(a => a.AttendanceDate >= today && a.AttendanceDate < tomorrow)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new RecentAttendanceItem
                {
                    EmployeeName = a.User.FirstName + " " + a.User.LastName,
                    SectionName = a.User.Section != null ? a.User.Section.Name : null,
                    Status = a.Status,
                    InTime = a.InTime,
                    OutTime = a.OutTime
                })
                .Take(8)
                .ToListAsync();

            var monthlyTrendRaw = await attendanceScope
                .Where(a => a.AttendanceDate >= monthStart && a.AttendanceDate < monthEndExclusive)
                .GroupBy(a => a.AttendanceDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Present = g.Count(a => a.Status == AttendanceStatus.Present),
                    Late = g.Count(a => a.Status == AttendanceStatus.Late),
                    Absent = g.Count(a => a.Status == AttendanceStatus.Absent),
                    OvertimeMinutes = g.Sum(a => a.OvertimeMinutes)
                })
                .ToListAsync();

            var attendedDaysToDate = await attendanceScope
                .Where(a => a.AttendanceDate >= monthStart && a.AttendanceDate < tomorrow)
                .Where(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late || a.Status == AttendanceStatus.HalfDay)
                .Select(a => a.AttendanceDate.Date)
                .Distinct()
                .CountAsync();

            var todayAttendance = statusCounts.Sum(x => x.Count);
            var presentCount = statusCounts.FirstOrDefault(x => x.Status == AttendanceStatus.Present)?.Count ?? 0;
            var lateCount = statusCounts.FirstOrDefault(x => x.Status == AttendanceStatus.Late)?.Count ?? 0;
            var absentCount = statusCounts.FirstOrDefault(x => x.Status == AttendanceStatus.Absent)?.Count ?? 0;
            var onLeaveCount = statusCounts.FirstOrDefault(x => x.Status == AttendanceStatus.Leave)?.Count ?? 0;
            var monthlyPresentCount = monthlyStatusCounts.FirstOrDefault(x => x.Status == AttendanceStatus.Present)?.Count ?? 0;
            var monthlyLateCount = monthlyStatusCounts.FirstOrDefault(x => x.Status == AttendanceStatus.Late)?.Count ?? 0;
            var monthlyAbsentCount = monthlyStatusCounts.FirstOrDefault(x => x.Status == AttendanceStatus.Absent)?.Count ?? 0;

            var overtimeMinutesToday = await todayAttendanceStats.SumAsync(a => a.OvertimeMinutes);
            var overtimeMinutesMonth = await attendanceScope
                .Where(a => a.AttendanceDate >= monthStart && a.AttendanceDate < monthEndExclusive)
                .SumAsync(a => a.OvertimeMinutes);

            var dailyMap = monthlyTrendRaw.ToDictionary(k => k.Date, v => v);
            var trendLabels = new List<string>();
            var presentTrend = new List<int>();
            var lateTrend = new List<int>();
            var absentTrend = new List<int>();
            var overtimeTrendHours = new List<int>();

            for (var day = monthStart; day <= today; day = day.AddDays(1))
            {
                trendLabels.Add(day.ToString("dd MMM"));
                if (dailyMap.TryGetValue(day.Date, out var row))
                {
                    presentTrend.Add(row.Present);
                    lateTrend.Add(row.Late);
                    absentTrend.Add(row.Absent);
                    overtimeTrendHours.Add((int)Math.Round(row.OvertimeMinutes / 60m));
                }
                else
                {
                    presentTrend.Add(0);
                    lateTrend.Add(0);
                    absentTrend.Add(0);
                    overtimeTrendHours.Add(0);
                }
            }

            var workingDaysToDate = Enumerable.Range(0, (today - monthStart).Days + 1)
                .Select(offset => monthStart.AddDays(offset))
                .Count(day => day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday);

            var attendanceTargetPercent = workingDaysToDate == 0
                ? 0
                : Math.Round((decimal)attendedDaysToDate / workingDaysToDate * 100, 1);

            // ServiceId search logic
            string? workerSearchMessage = null;
            WorkerHistorySearchViewModel? workerHistorySearchResult = null;

            if (!string.IsNullOrWhiteSpace(serviceId))
            {
                var normalizedServiceId = serviceId.Trim().ToUpperInvariant();

                var worker = await _context.Users
                    .Include(u => u.Section)
                    .FirstOrDefaultAsync(u =>
                        u.IsActive &&
                        u.ServiceId != null &&
                        u.ServiceId.ToUpper() == normalizedServiceId);

                if (worker == null)
                {
                    workerSearchMessage = $"Worker with Service ID {normalizedServiceId} not found.";
                }
                else
                {
                    var history = await _context.Attendances
                        .Where(a => a.UserId == worker.Id)
                        .OrderByDescending(a => a.AttendanceDate)
                        .Take(30)
                        .ToListAsync();

                    workerHistorySearchResult = new WorkerHistorySearchViewModel
                    {
                        WorkerId = worker.Id,
                        ServiceId = worker.ServiceId ?? "-",
                        FullName = worker.FullName,
                        Email = worker.Email,
                        SectionName = worker.Section != null ? worker.Section.Name : "Unassigned",
                        TotalWorkedHours = history.Sum(a => a.TotalWorkedMinutes) / 60m,
                        TotalOTHours = history.Sum(a => a.OvertimeMinutes) / 60m,
                        History = history.Select(a => new WorkerAttendanceHistoryRowViewModel
                        {
                            Date = a.AttendanceDate,
                            InTime = a.InTime.HasValue ? a.InTime.Value.ToString(@"hh\:mm") : "-",
                            OutTime = a.OutTime.HasValue ? a.OutTime.Value.ToString(@"hh\:mm") : "-",
                            WorkedHours = a.TotalWorkedMinutes > 0 ? (a.TotalWorkedMinutes / 60m).ToString("F2") : "-",
                            OTHours = a.OvertimeMinutes > 0 ? (a.OvertimeMinutes / 60m).ToString("F2") : "-",
                            Status = a.Status
                        }).ToList()
                    };

                    if (!history.Any())
                    {
                        workerSearchMessage = "Worker found, but no attendance history exists.";
                    }
                }
            }

            // Selected date attendance logic
            SelectedDateAttendanceViewModel? selectedDateAttendance = null;
            if (!string.IsNullOrWhiteSpace(serviceId) && selectedDate.HasValue && workerHistorySearchResult != null)
            {
                Console.WriteLine($"[SELECTED DATE] Processing selected date {selectedDate.Value:yyyy-MM-dd} for worker {serviceId}");

                var selectedDay = selectedDate.Value.Date;
                var attendanceOnDate = await _context.Attendances
                    .Where(a =>
                        a.UserId == workerHistorySearchResult.WorkerId &&
                        a.AttendanceDate >= selectedDay &&
                        a.AttendanceDate < selectedDay.AddDays(1))
                    .FirstOrDefaultAsync();

                if (attendanceOnDate != null)
                {
                    Console.WriteLine($"[SELECTED DATE] Found attendance record for {selectedDate.Value:yyyy-MM-dd}");
                    
                    // Calculate worked hours
                    var workedHours = "-";
                    if (attendanceOnDate.TotalWorkedMinutes > 0)
                    {
                        var hours = attendanceOnDate.TotalWorkedMinutes / 60;
                        var minutes = attendanceOnDate.TotalWorkedMinutes % 60;
                        workedHours = $"{hours}:{minutes:D2}";
                    }

                    // Calculate OT hours
                    var otHours = "-";
                    if (attendanceOnDate.OvertimeMinutes > 0)
                    {
                        var hours = attendanceOnDate.OvertimeMinutes / 60;
                        var minutes = attendanceOnDate.OvertimeMinutes % 60;
                        otHours = $"{hours}:{minutes:D2}";
                    }

                    // Calculate late by
                    var lateBy = "-";
                    if (attendanceOnDate.InTime.HasValue && attendanceOnDate.Status == "Late")
                    {
                        var workStartTime = new TimeSpan(8, 30, 0); // 8:30 AM
                        var actualTime = attendanceOnDate.InTime.Value;
                        if (actualTime > workStartTime)
                        {
                            var diff = actualTime - workStartTime;
                            lateBy = $"{diff.Hours}h {diff.Minutes}m";
                        }
                    }

                    // Determine current state
                    var currentState = "Completed";
                    if (attendanceOnDate.Status == "Leave")
                    {
                        currentState = "On Leave";
                    }
                    else if (attendanceOnDate.OutTime.HasValue)
                    {
                        if (attendanceOnDate.OvertimeMinutes > 0)
                        {
                            currentState = "Working OT";
                        }
                        else
                        {
                            currentState = "Completed";
                        }
                    }
                    else if (attendanceOnDate.InTime.HasValue)
                    {
                        currentState = "Working";
                    }

                    selectedDateAttendance = new SelectedDateAttendanceViewModel
                    {
                        WorkerName = workerHistorySearchResult.FullName,
                        ServiceId = workerHistorySearchResult.ServiceId,
                        SectionName = workerHistorySearchResult.SectionName,
                        SelectedDate = selectedDate.Value,
                        InTime = attendanceOnDate.InTime?.ToString(@"hh\:mm") ?? "-",
                        OutTime = attendanceOnDate.OutTime?.ToString(@"hh\:mm") ?? "-",
                        WorkedHours = workedHours,
                        OTHours = otHours,
                        Status = attendanceOnDate.Status,
                        LateBy = lateBy,
                        CurrentState = currentState,
                        HasRecord = true
                    };
                }
                else
                {
                    Console.WriteLine($"[SELECTED DATE] No attendance record found for {selectedDate.Value:yyyy-MM-dd}");
                    
                    selectedDateAttendance = new SelectedDateAttendanceViewModel
                    {
                        WorkerName = workerHistorySearchResult.FullName,
                        ServiceId = workerHistorySearchResult.ServiceId,
                        SectionName = workerHistorySearchResult.SectionName,
                        SelectedDate = selectedDate.Value,
                        InTime = "-",
                        OutTime = "-",
                        WorkedHours = "-",
                        OTHours = "-",
                        Status = "Absent",
                        LateBy = "-",
                        CurrentState = "Absent",
                        HasRecord = false
                    };
                }
            }

            var viewModel = new DashboardViewModel
            {
                SelectedDate = today,
                TotalUsers = totalUsers,
                TodayAttendance = todayAttendance,
                PresentCount = presentCount,
                LateCount = lateCount,
                AbsentCount = absentCount,
                OnLeaveCount = onLeaveCount,
                OvertimeMinutesToday = overtimeMinutesToday,
                OvertimeMinutesMonth = overtimeMinutesMonth,
                WorkingDaysToDate = workingDaysToDate,
                AttendedDaysToDate = attendedDaysToDate,
                MonthlyPresentCount = monthlyPresentCount,
                MonthlyLateCount = monthlyLateCount,
                MonthlyAbsentCount = monthlyAbsentCount,
                AttendanceTargetPercent = attendanceTargetPercent,
                ScopeTitle = isSuperAdmin
                    ? "Organization-wide attendance"
                    : isGM
                        ? "All Sections (View Only)"
                        : isLeaveAgent
                            ? $"{currentUser.Section?.Name ?? "Section"} Overview"
                            : isAdmin
                                ? $"{currentUser.Section?.Name ?? "Section"} attendance"
                                : "My attendance overview",
                IsSuperAdmin = isSuperAdmin || isGM,
                IsAdmin = isAdmin,
                IsWorker = isWorker,
                IsGM = isGM,
                IsLeaveAgent = isLeaveAgent,
                TrendLabels = trendLabels,
                PresentTrend = presentTrend,
                LateTrend = lateTrend,
                AbsentTrend = absentTrend,
                OvertimeTrendHours = overtimeTrendHours,
                DistributionLabels = new List<string>
                {
                    AttendanceStatus.Present,
                    AttendanceStatus.Late,
                    AttendanceStatus.Absent,
                    AttendanceStatus.Leave
                },
                DistributionValues = new List<int>
                {
                    monthlyPresentCount,
                    monthlyLateCount,
                    monthlyAbsentCount,
                    monthlyStatusCounts.FirstOrDefault(x => x.Status == AttendanceStatus.Leave)?.Count ?? 0
                },
                SectionSnapshots = sectionSnapshots,
                RecentAttendance = recentAttendance,
                UserName = User.Identity?.Name ?? string.Empty,
                
                // Search results
                SearchServiceId = serviceId,
                WorkerSearchMessage = workerSearchMessage,
                WorkerHistorySearchResult = workerHistorySearchResult,
                SelectedDateAttendance = selectedDateAttendance
            };

            // Build OT dashboard data for Admin/SuperAdmin/GM/LeaveAgent
            await BuildOTDashboardViewModelAsync(currentUser, today, viewModel);

            // Leave Agents get a dedicated focused dashboard view
            if (isLeaveAgent)
            {
                return View("LeaveAgentDashboard", viewModel);
            }

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> SearchWorkerOt([FromQuery] WorkerOtSearchRequestViewModel request)
        {
            _logger.LogDebug("[WORKER SEARCH AJAX] serviceId={ServiceId}, selectedDate={SelectedDate}, singleDate={SingleDate}, fromDate={FromDate}, toDate={ToDate}",
                request.ServiceId,
                request.SelectedDate?.ToString("yyyy-MM-dd"),
                request.SingleDate?.ToString("yyyy-MM-dd"),
                request.FromDate?.ToString("yyyy-MM-dd"),
                request.ToDate?.ToString("yyyy-MM-dd"));

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var currentUser = await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Section)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (currentUser == null)
            {
                return Unauthorized();
            }

            if (currentUser.Role?.Name != RoleNames.Admin && !RoleNames.HasEngineerPrivileges(currentUser.Role?.Name))
            {
                return Forbid();
            }

            var viewModel = await BuildAdminWorkerSearchViewModelAsync(currentUser, request);
            return PartialView("_WorkerOtSearchResults", viewModel);
        }

        private async Task<AdminDashboardViewModel> BuildAdminWorkerSearchViewModelAsync(User currentUser, WorkerOtSearchRequestViewModel request)
        {
            var adminDashboardViewModel = new AdminDashboardViewModel
            {
                HasSection = true,
                SectionName = currentUser.Section?.Name ?? string.Empty,
                SearchServiceId = request.NormalizedServiceId,
                SingleDate = (request.SingleDate ?? request.SelectedDate ?? DateTime.Today).Date,
                RangeFromDate = (request.FromDate ?? DateTime.Today.AddDays(-7)).Date,
                RangeToDate = (request.ToDate ?? DateTime.Today).Date
            };

            var isSuperAdminOrGM = _authService.IsSuperAdminOrGM(currentUser);

            // Build sections dropdown based on role
            if (isSuperAdminOrGM)
            {
                // SuperAdmin/GM: show all sections including "All Sections" (Id=-1)
                var sections = new List<SelectListItem>
                {
                    new SelectListItem { Value = "-1", Text = "All Sections" }
                };

                var dbSections = await _context.Sections
                    .AsNoTracking()
                    .Where(s => s.IsActive && s.Id != -1)
                    .OrderBy(s => s.Name)
                    .Select(s => new SelectListItem
                    {
                        Value = s.Id.ToString(),
                        Text = s.Name
                    })
                    .ToListAsync();

                sections.AddRange(dbSections);
                adminDashboardViewModel.Sections = sections;

                // SuperAdmin/GM: show all workers across all sections
                adminDashboardViewModel.WorkerServiceOptions = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.IsActive && !string.IsNullOrWhiteSpace(u.ServiceId))
                    .OrderBy(u => u.ServiceId)
                    .Select(u => new SelectListItem
                    {
                        Value = u.ServiceId!,
                        Text = u.ServiceId! + " - " + u.FirstName + " " + u.LastName
                    })
                    .ToListAsync();
            }
            else
            {
                // Normal users: show only their assigned section
                var sectionId = currentUser.SectionId;
                if (!SectionIds.IsAssignable(sectionId))
                {
                    adminDashboardViewModel.HasSection = false;
                    adminDashboardViewModel.NoSectionMessage = "No valid section assigned to this Admin. Section 0 is reserved for SuperAdmin and GM only.";
                    return adminDashboardViewModel;
                }

                var userSection = await _context.Sections
                    .AsNoTracking()
                    .Where(s => s.Id == sectionId!.Value)
                    .Select(s => new SelectListItem
                    {
                        Value = s.Id.ToString(),
                        Text = s.Name
                    })
                    .FirstOrDefaultAsync();

                adminDashboardViewModel.Sections = userSection != null ? new List<SelectListItem> { userSection } : new List<SelectListItem>();

                // Normal users: show only workers from their section
                adminDashboardViewModel.WorkerServiceOptions = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.IsActive && u.SectionId == sectionId && !string.IsNullOrWhiteSpace(u.ServiceId))
                    .OrderBy(u => u.ServiceId)
                    .Select(u => new SelectListItem
                    {
                        Value = u.ServiceId!,
                        Text = u.ServiceId! + " - " + u.FirstName + " " + u.LastName
                    })
                    .ToListAsync();
            }

            if (adminDashboardViewModel.RangeFromDate > adminDashboardViewModel.RangeToDate)
            {
                var tempDate = adminDashboardViewModel.RangeFromDate;
                adminDashboardViewModel.RangeFromDate = adminDashboardViewModel.RangeToDate;
                adminDashboardViewModel.RangeToDate = tempDate;
            }

            if (string.IsNullOrWhiteSpace(request.NormalizedServiceId))
            {
                return adminDashboardViewModel;
            }

            // Search worker based on role
            User? worker;
            if (isSuperAdminOrGM)
            {
                // SuperAdmin/GM: search across all sections
                worker = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.Section)
                    .FirstOrDefaultAsync(u =>
                        u.IsActive &&
                        u.ServiceId != null &&
                        u.ServiceId.ToUpper() == request.NormalizedServiceId);
            }
            else
            {
                // Normal users: search only in their section
                var sectionId = currentUser.SectionId;
                if (!SectionIds.IsAssignable(sectionId))
                {
                    adminDashboardViewModel.WorkerSearchMessage = "No valid section assigned to your account.";
                    return adminDashboardViewModel;
                }

                worker = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.Section)
                    .FirstOrDefaultAsync(u =>
                        u.IsActive &&
                        u.SectionId == sectionId &&
                        u.ServiceId != null &&
                        u.ServiceId.ToUpper() == request.NormalizedServiceId);
            }

            if (worker == null)
            {
                adminDashboardViewModel.WorkerSearchMessage = "No worker found for this Service ID.";
                return adminDashboardViewModel;
            }

            adminDashboardViewModel.ShowWorkerResultCards = true;
            adminDashboardViewModel.SearchServiceId = request.NormalizedServiceId;
            adminDashboardViewModel.WorkerProfile = new WorkerOtProfileViewModel
            {
                FullName = worker.FullName,
                ServiceId = worker.ServiceId ?? "-",
                SectionName = worker.Section?.Name ?? "Unassigned",
                Initials = BuildInitials(worker.FirstName, worker.LastName)
            };

            var minDate = new[] { adminDashboardViewModel.SingleDate, adminDashboardViewModel.RangeFromDate }.Min();
            var maxDate = new[] { adminDashboardViewModel.SingleDate, adminDashboardViewModel.RangeToDate }.Max();

            var workerRecords = await _context.Attendances
                .AsNoTracking()
                .Where(a =>
                    a.UserId == worker.Id &&
                    a.AttendanceDate >= minDate &&
                    a.AttendanceDate < maxDate.AddDays(1))
                .OrderBy(a => a.AttendanceDate)
                .ThenBy(a => a.InTime)
                .ToListAsync();

            var singleDateRecord = workerRecords
                .Where(a => a.AttendanceDate.Date == adminDashboardViewModel.SingleDate)
                .OrderByDescending(a => a.AttendanceDate)
                .ThenByDescending(a => a.OutTime)
                .FirstOrDefault(a => a.InTime.HasValue && a.OutTime.HasValue)
                ?? workerRecords
                    .Where(a => a.AttendanceDate.Date == adminDashboardViewModel.SingleDate)
                    .OrderByDescending(a => a.AttendanceDate)
                    .ThenByDescending(a => a.OutTime)
                    .FirstOrDefault();

            if (singleDateRecord == null)
            {
                adminDashboardViewModel.SingleDateMessage = "No overtime record found for this selected date.";
            }
            else
            {
                adminDashboardViewModel.SingleDateOtDetails = new WorkerSingleDateOtDetailsViewModel
                {
                    Date = singleDateRecord.AttendanceDate.Date,
                    OvertimeInTime = singleDateRecord.InTime?.ToString(@"hh\:mm") ?? "-",
                    OvertimeOutTime = singleDateRecord.OutTime?.ToString(@"hh\:mm") ?? "-",
                    OvertimeHours = singleDateRecord.InTime.HasValue && singleDateRecord.OutTime.HasValue
                        ? FormatDurationMinutes(CalculateDurationMinutes(singleDateRecord.InTime, singleDateRecord.OutTime))
                        : "-"
                };
            }

            var rangeRecords = workerRecords
                .Where(a => a.AttendanceDate.Date >= adminDashboardViewModel.RangeFromDate && a.AttendanceDate.Date <= adminDashboardViewModel.RangeToDate)
                .OrderBy(a => a.AttendanceDate)
                .ThenBy(a => a.InTime)
                .ToList();

            adminDashboardViewModel.DateRangeRows = rangeRecords.Select(a => new WorkerOtRangeRowViewModel
            {
                Date = a.AttendanceDate.Date,
                OvertimeInTime = a.InTime?.ToString(@"hh\:mm") ?? "-",
                OvertimeOutTime = a.OutTime?.ToString(@"hh\:mm") ?? "-",
                OvertimeHours = a.InTime.HasValue && a.OutTime.HasValue
                    ? FormatDurationMinutes(CalculateDurationMinutes(a.InTime, a.OutTime))
                    : "-",
                Status = a.Status
            }).ToList();

            var validRangeRecords = rangeRecords
                .Where(a => a.InTime.HasValue && a.OutTime.HasValue)
                .ToList();

            if (!validRangeRecords.Any())
            {
                adminDashboardViewModel.DateRangeMessage = "No overtime records found for this selected date range.";
                return adminDashboardViewModel;
            }

            var totalDays = validRangeRecords.Select(a => a.AttendanceDate.Date).Distinct().Count();
            var totalOvertimeMinutes = validRangeRecords.Sum(a => CalculateDurationMinutes(a.InTime, a.OutTime));

            adminDashboardViewModel.DateRangeChartData = validRangeRecords
                .OrderBy(a => a.AttendanceDate)
                .ThenBy(a => a.InTime)
                .Select(a => new DailyOtChartPointViewModel
                {
                    DateLabel = a.AttendanceDate.ToString("dd MMM"),
                    OTHours = Math.Round(CalculateDurationMinutes(a.InTime, a.OutTime) / 60m, 2)
                })
                .ToList();

            adminDashboardViewModel.DateRangeOtSummary = new WorkerDateRangeOtSummaryViewModel
            {
                FromDate = adminDashboardViewModel.RangeFromDate,
                ToDate = adminDashboardViewModel.RangeToDate,
                TotalDays = totalDays,
                TotalOvertimeMinutes = totalOvertimeMinutes,
                TotalOvertimeDisplay = FormatDurationMinutes(totalOvertimeMinutes)
            };

            return adminDashboardViewModel;
        }

        [HttpGet]
        public async Task<IActionResult> DownloadWorkerRecentOtPdf(DateTime? selectedDate)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "Account");
            }
    
            var currentUser = await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Section)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (currentUser.Role.Name != RoleNames.Worker)
            {
                return Forbid();
            }

            var searchDate = selectedDate?.Date ?? DateTime.Today;
            var monthStartDate = new DateTime(searchDate.Year, searchDate.Month, 1);
            var rangeEndExclusive = searchDate.AddDays(1);

            var workerRangeRecords = await _context.Attendances
                .AsNoTracking()
                .Where(a =>
                    a.UserId == currentUser.Id &&
                    a.AttendanceDate >= monthStartDate &&
                    a.AttendanceDate < rangeEndExclusive)
                .OrderByDescending(a => a.AttendanceDate)
                .ThenByDescending(a => a.OutTime)
                .ToListAsync();

            var workerOtDashboard = BuildWorkerOtDashboardViewModel(currentUser, monthStartDate, searchDate, workerRangeRecords);
            var pdfBytes = WorkerOtPdfReportBuilder.BuildRecentOtRecordsPdf(workerOtDashboard, DateTime.Now);
            var fileName = $"recent-ot-records-{searchDate:yyyyMMdd}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> GetWorkerAttendanceByDate(string serviceId, DateTime date)
        {
            Console.WriteLine($"[WORKER DATE SEARCH] serviceId: {serviceId}, date: {date:yyyy-MM-dd}");

            if (string.IsNullOrWhiteSpace(serviceId))
            {
                return Json(new { success = false, message = "Service ID is required." });
            }

            var normalizedServiceId = serviceId.Trim().ToUpperInvariant();

            // Step 1: Find worker from Users table by ServiceId
            var worker = await _context.Users
                .Include(u => u.Section)
                .FirstOrDefaultAsync(u =>
                    u.IsActive &&
                    u.ServiceId != null &&
                    u.ServiceId.ToUpper() == normalizedServiceId);

            if (worker == null)
            {
                Console.WriteLine($"[WORKER DATE SEARCH] Worker with ServiceId {normalizedServiceId} not found");
                return Json(new { success = false, message = "Worker not found" });
            }

            Console.WriteLine($"[WORKER DATE SEARCH] Found worker: {worker.FirstName} {worker.LastName} (ID: {worker.Id})");

            // Step 2: Find attendance from Attendances using UserId and date range
            var targetDate = date.Date;
            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a =>
                    a.UserId == worker.Id &&
                    a.AttendanceDate >= targetDate &&
                    a.AttendanceDate < targetDate.AddDays(1));

            if (attendance == null)
            {
                Console.WriteLine($"[WORKER DATE SEARCH] No attendance record found for {serviceId} on {targetDate:yyyy-MM-dd}");
                return Json(new { 
                    success = false,
                    workerName = $"{worker.FirstName} {worker.LastName}",
                    serviceId = worker.ServiceId,
                    date = targetDate,
                    message = $"No attendance record found for {normalizedServiceId} on {targetDate:MMM dd, yyyy}."
                });
            }

            Console.WriteLine($"[WORKER DATE SEARCH] Found attendance record: InTime={attendance.InTime}, OutTime={attendance.OutTime}, Status={attendance.Status}");

            // Step 3: Use AttendanceCalculationService to recalculate attendance with standardized rules
            var calculationResult = _calculationService.CalculateAttendance(
                attendance.AttendanceDate, 
                attendance.InTime, 
                attendance.OutTime, 
                attendance.Status);

            Console.WriteLine($"[WORKER DATE SEARCH] Recalculated: Status={calculationResult.Status}, TotalWorkedMinutes={calculationResult.TotalWorkedMinutes}, OvertimeMinutes={calculationResult.OvertimeMinutes}, LateByMinutes={calculationResult.LateByMinutes}, EarlyByMinutes={calculationResult.EarlyByMinutes}");

            // Step 4: Return success response with correctly calculated record object
            return Json(new { 
                success = true,
                record = new
                {
                    workerName = $"{worker.FirstName} {worker.LastName}",
                    serviceId = worker.ServiceId,
                    section = worker.Section?.Name ?? "Unassigned",
                    date = calculationResult.AttendanceDate,
                    loginTime = calculationResult.InTime,
                    logoutTime = calculationResult.OutTime,
                    workedMinutes = calculationResult.TotalWorkedMinutes,
                    workedHoursText = calculationResult.WorkedHoursText,
                    regularMinutes = calculationResult.RegularWorkedMinutes,
                    regularHoursText = calculationResult.RegularHoursText,
                    overtimeMinutes = calculationResult.OvertimeMinutes,
                    overtimeHoursText = calculationResult.OvertimeHoursText,
                    status = calculationResult.Status,
                    lateByText = calculationResult.LateByText,
                    earlyByText = calculationResult.EarlyByText,
                    currentState = calculationResult.CurrentState
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWorker([FromBody] CreateWorkerViewModel model)
        {
            try
            {
                model.WorkerName = model.WorkerName?.Trim() ?? string.Empty;
                model.ServiceId = model.ServiceId?.Trim() ?? string.Empty;
                model.Email = model.Email?.Trim() ?? string.Empty;
                model.Phone = model.Phone?.Trim() ?? string.Empty;

                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, errors = GetModelStateErrors() });
                }

                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Json(new { success = false, message = "User not authenticated." });
                }

                var currentUser = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);

                if (currentUser == null)
                {
                    return Json(new { success = false, message = "User not found or inactive." });
                }

                // Check if user has permission to create workers
                if (currentUser.Role == null ||
                    (currentUser.Role.Name != RoleNames.Admin && currentUser.Role.Name != RoleNames.SuperAdmin))
                {
                    return Json(new { success = false, message = "You don't have permission to create workers." });
                }

                if (string.IsNullOrWhiteSpace(model.WorkerName))
                {
                    return Json(new { success = false, errors = new { WorkerName = new[] { "Worker name is required." } } });
                }

                if (!SectionIds.IsAssignable(currentUser.SectionId))
                {
                    return Json(new { success = false, message = "No valid section is assigned to your account. Workers cannot be added to section 0." });
                }

                var sectionId = currentUser.SectionId!.Value;

                var section = await _context.Sections
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == sectionId && s.IsActive && s.Id > 0);

                if (section == null)
                {
                    return Json(new { success = false, message = "Your assigned section is not available." });
                }

                // Check for duplicate Service ID
                var normalizedServiceId = model.ServiceId.ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(normalizedServiceId))
                {
                    return Json(new { success = false, errors = new { ServiceId = new[] { "Service ID is required." } } });
                }

                var existingServiceId = await _context.Users
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .AnyAsync(u => u.ServiceId != null && u.ServiceId.ToUpper() == normalizedServiceId);

                if (existingServiceId)
                {
                    return Json(new { success = false, errors = new { ServiceId = new[] { "A worker with this Service ID already exists." } } });
                }

                // Check for duplicate Email
                var existingEmail = await _context.Users
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .AnyAsync(u => u.Email.ToUpper() == model.Email.ToUpper());

                if (existingEmail)
                {
                    return Json(new { success = false, errors = new { Email = new[] { "A worker with this email address already exists." } } });
                }

                // Get Worker role
                var workerRole = await _context.Roles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Name == RoleNames.Worker);

                if (workerRole == null)
                {
                    return Json(new { success = false, message = "Worker role not found." });
                }

                // Generate secure temporary password
                var temporaryPassword = GenerateSecureTemporaryPassword();
                var passwordHash = HashPassword(temporaryPassword);

                var nameParts = model.WorkerName
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var firstName = nameParts.Length > 0 ? nameParts[0] : model.WorkerName;
                var lastName = nameParts.Length > 1 ? string.Join(' ', nameParts.Skip(1)) : string.Empty;

                // Create new user
                var newUser = new User
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = model.Email,
                    PasswordHash = passwordHash,
                    Phone = model.Phone,
                    ServiceId = normalizedServiceId,
                    SectionId = sectionId,
                    RoleId = workerRole.Id,
                    IsActive = true,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                Console.WriteLine($"[WORKER CREATION] New worker created: ServiceId={model.ServiceId}, Email={model.Email}, Section={section.Name}, CreatedBy={currentUser.Email}");

                try
                {
                    await _emailService.SendLoginCredentialsAsync(model.Email, newUser.FullName, normalizedServiceId, temporaryPassword, "Worker");
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"[EMAIL ERROR] Failed to send email to {model.Email}: {emailEx.Message}");
                }

                return Json(new { 
                    success = true, 
                    message = "Worker created successfully. Login credentials sent to their email.",
                    workerId = newUser.Id
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WORKER CREATION ERROR] {ex.Message}");
                return Json(new { success = false, message = "An error occurred while creating the worker. Please try again." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserViewModel model)
        {
            try
            {
                model.ServiceId = model.ServiceId?.Trim() ?? string.Empty;

                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, errors = GetModelStateErrors() });
                }

                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdClaim, out var currentUserId))
                {
                    return Json(new { success = false, message = "User not authenticated." });
                }

                var currentUser = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);

                if (currentUser == null)
                {
                    return Json(new { success = false, message = "User not found or inactive." });
                }

                if (currentUser.Role == null ||
                    (currentUser.Role.Name != RoleNames.Admin && currentUser.Role.Name != RoleNames.SuperAdmin))
                {
                    return Json(new { success = false, message = "You don't have permission to delete users." });
                }

                var isSuperAdminOrGM = _authService.IsSuperAdminOrGM(currentUser);

                // Regular Admin must have a real section assigned (not 0 or -1)
                if (!isSuperAdminOrGM && !SectionIds.IsAssignable(currentUser.SectionId))
                {
                    return Json(new { success = false, message = "No valid section is assigned to your account." });
                }

                if (string.IsNullOrWhiteSpace(model.ServiceId))
                {
                    return Json(new { success = false, errors = new { ServiceId = new[] { "Service ID is required." } } });
                }

                var normalizedServiceId = model.ServiceId.ToUpperInvariant();
                
                // Search user based on role
                User? userToDelete;
                if (isSuperAdminOrGM)
                {
                    // SuperAdmin/GM: can delete from any section
                    userToDelete = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u =>
                            u.IsActive &&
                            u.ServiceId.ToUpper() == normalizedServiceId);
                }
                else
                {
                    // Regular Admin: can only delete from their section
                    userToDelete = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u =>
                            u.IsActive &&
                            u.ServiceId.ToUpper() == normalizedServiceId &&
                            u.SectionId == currentUser.SectionId!.Value);
                }

                if (userToDelete == null)
                {
                    return Json(new { success = false, message = "No active user found for that Service ID in your section." });
                }

                if (userToDelete.Id == currentUser.Id)
                {
                    return Json(new { success = false, message = "You cannot delete your own account." });
                }

                userToDelete.IsActive = false;
                userToDelete.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "User deleted successfully.",
                    serviceId = userToDelete.ServiceId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[USER DELETE ERROR] {ex.Message}");
                return Json(new { success = false, message = "An error occurred while deleting the user. Please try again." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetNextServiceId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var currentUserId))
            {
                return Json(new { success = false, message = "User not authenticated." });
            }

            var currentUser = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);

            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not found or inactive." });
            }

            if (currentUser.Role == null ||
                (currentUser.Role.Name != RoleNames.Admin && currentUser.Role.Name != RoleNames.SuperAdmin))
            {
                return Json(new { success = false, message = "You don't have permission to generate a Service ID." });
            }

            if (!SectionIds.IsAssignable(currentUser.SectionId))
            {
                return Json(new { success = false, message = "No valid section is assigned to your account." });
            }

            var nextServiceId = await GetNextAvailableEmployeeServiceIdAsync(HttpContext.RequestAborted);
            return Json(new { success = true, serviceId = nextServiceId });
        }

        private static WorkerOtDashboardViewModel BuildWorkerOtDashboardViewModel(
            User currentUser,
            DateTime monthStartDate,
            DateTime searchDate,
            List<Attendance> rangeRecords)
        {
            var validDurationRecords = rangeRecords
                .Where(a => a.InTime.HasValue && a.OutTime.HasValue)
                .Select(a => new
                {
                    Record = a,
                    DurationMinutes = CalculateDurationMinutes(a.InTime, a.OutTime)
                })
                .Where(x => x.DurationMinutes > 0)
                .ToList();

            var durationByDate = validDurationRecords
                .GroupBy(x => x.Record.AttendanceDate.Date)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.DurationMinutes) / 60m);

            var trendPoints = new List<WorkerOtTrendPointViewModel>();
            for (var day = monthStartDate; day <= searchDate; day = day.AddDays(1))
            {
                trendPoints.Add(new WorkerOtTrendPointViewModel
                {
                    DateLabel = day.ToString("dd MMM"),
                    OtHours = Math.Round(durationByDate.GetValueOrDefault(day.Date, 0m), 2)
                });
            }

            var recordsForSearchedDate = rangeRecords
                .Where(a => a.AttendanceDate.Date == searchDate.Date)
                .OrderByDescending(a => a.OutTime)
                .ThenByDescending(a => a.InTime)
                .ToList();

            var searchedDateRecord = recordsForSearchedDate
                .FirstOrDefault(a => a.InTime.HasValue && a.OutTime.HasValue)
                ?? recordsForSearchedDate.FirstOrDefault();

            // If no OT record exists on searched date, keep "-" values while still showing cumulative month totals.
            var searchedDateDuration = searchedDateRecord?.InTime.HasValue == true && searchedDateRecord?.OutTime.HasValue == true
                ? FormatDurationMinutes(CalculateDurationMinutes(searchedDateRecord.InTime, searchedDateRecord.OutTime))
                : "-";

            var totalOtMinutes = validDurationRecords.Sum(x => x.DurationMinutes);
            var otDays = validDurationRecords.Count;
            var averageOtHoursPerDay = otDays == 0
                ? 0
                : Math.Round((totalOtMinutes / 60m) / otDays, 2);

            return new WorkerOtDashboardViewModel
            {
                MonthStartDate = monthStartDate,
                SearchDate = searchDate,
                Initials = BuildInitials(currentUser.FirstName, currentUser.LastName),
                FullName = currentUser.FullName,
                Email = currentUser.Email,
                SectionName = currentUser.Section?.Name ?? "Unassigned",
                ServiceId = currentUser.ServiceId ?? "-",
                SearchedDateOtInTime = searchedDateRecord?.InTime?.ToString(@"hh\:mm") ?? "-",
                SearchedDateOtOutTime = searchedDateRecord?.OutTime?.ToString(@"hh\:mm") ?? "-",
                SearchedDateOtDuration = searchedDateDuration,
                TotalOtMinutes = totalOtMinutes,
                OtDays = otDays,
                AverageOtHoursPerDay = averageOtHoursPerDay,
                TotalRecords = rangeRecords.Count,
                TotalOtDurationDisplay = FormatDurationMinutes(totalOtMinutes),
                TrendPoints = trendPoints,
                RecentOtRecords = rangeRecords
                    .OrderByDescending(a => a.AttendanceDate)
                    .ThenByDescending(a => a.OutTime)
                    .Select(a => new WorkerOtRecordRowViewModel
                    {
                        Date = a.AttendanceDate.Date,
                        OtInTime = a.InTime?.ToString(@"hh\:mm") ?? "-",
                        OtOutTime = a.OutTime?.ToString(@"hh\:mm") ?? "-",
                        OtDuration = a.InTime.HasValue && a.OutTime.HasValue
                            ? FormatDurationMinutes(CalculateDurationMinutes(a.InTime, a.OutTime))
                            : "-"
                    })
                    .ToList()
            };
        }

        private async Task<string> GetNextAvailableEmployeeServiceIdAsync(CancellationToken cancellationToken)
        {
            var serviceIds = await _context.Users
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(u => u.ServiceId != null)
                .Select(u => u.ServiceId!)
                .ToListAsync(cancellationToken);

            var nextNumber = serviceIds
                .Select(ParseEmployeeServiceNumber)
                .Where(number => number.HasValue)
                .Select(number => number!.Value)
                .DefaultIfEmpty(0)
                .Max() + 1;

            while (true)
            {
                var candidate = BuildEmployeeServiceId(nextNumber);
                var inUse = await _context.Users
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .AnyAsync(u => u.ServiceId != null && u.ServiceId.ToUpper() == candidate, cancellationToken);

                if (!inUse)
                {
                    return candidate;
                }

                nextNumber++;
            }
        }

        private static int? ParseEmployeeServiceNumber(string serviceId)
        {
            var normalized = serviceId.Trim().ToUpperInvariant();
            if (!normalized.StartsWith("EMP", StringComparison.Ordinal))
            {
                return null;
            }

            var digitsOnly = new string(normalized
                .Skip(3)
                .TakeWhile(char.IsDigit)
                .ToArray());

            if (string.IsNullOrWhiteSpace(digitsOnly))
            {
                return null;
            }

            return int.TryParse(digitsOnly, out var parsed) ? parsed : null;
        }

        private static string BuildEmployeeServiceId(int number)
        {
            return $"EMP{number:D3}";
        }

        private string GenerateSecureTemporaryPassword()
        {
            const string upperChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lowerChars = "abcdefghijkmnpqrstuvwxyz";
            const string digitChars = "23456789";
            const string specialChars = "!@#$%^&*";
            
            var random = new Random();
            var password = new System.Text.StringBuilder();
            
            // Ensure at least one character from each category
            password.Append(upperChars[random.Next(upperChars.Length)]);
            password.Append(lowerChars[random.Next(lowerChars.Length)]);
            password.Append(digitChars[random.Next(digitChars.Length)]);
            password.Append(specialChars[random.Next(specialChars.Length)]);
            
            // Add remaining characters to reach 12 characters total
            const string allChars = upperChars + lowerChars + digitChars + specialChars;
            for (int i = 4; i < 12; i++)
            {
                password.Append(allChars[random.Next(allChars.Length)]);
            }
            
            // Shuffle the password characters
            var passwordArray = password.ToString().ToCharArray();
            for (int i = passwordArray.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                char temp = passwordArray[i];
                passwordArray[i] = passwordArray[j];
                passwordArray[j] = temp;
            }
            
            return new string(passwordArray);
        }

        private Dictionary<string, string[]> GetModelStateErrors()
        {
            var errors = new Dictionary<string, string[]>();
            foreach (var key in ModelState.Keys)
            {
                var state = ModelState[key];
                if (state != null && state.Errors.Any())
                {
                    errors[key] = state.Errors.Select(e => e.ErrorMessage).ToArray();
                }
            }
            return errors;
        }

        private static int CalculateOtDurationMinutes(DateTime date, TimeSpan? otInTime, TimeSpan? otOutTime)
        {
            if (!otInTime.HasValue || !otOutTime.HasValue)
            {
                return 0;
            }

            var start = date.Date + otInTime.Value;
            var end = date.Date + otOutTime.Value;
            if (end < start)
            {
                end = end.AddDays(1);
            }

            return Math.Max(0, (int)(end - start).TotalMinutes);
        }

        private static int CalculateDurationMinutes(TimeSpan? inTime, TimeSpan? outTime)
        {
            if (!inTime.HasValue || !outTime.HasValue)
            {
                return 0;
            }

            var safeOutTime = outTime.Value;
            if (safeOutTime < inTime.Value)
            {
                safeOutTime = safeOutTime.Add(TimeSpan.FromDays(1));
            }

            return Math.Max(0, (int)(safeOutTime - inTime.Value).TotalMinutes);
        }

        private static string FormatDurationMinutes(int minutes)
        {
            if (minutes <= 0)
            {
                return "0h 0m";
            }

            var totalHours = minutes / 60;
            var remainingMinutes = minutes % 60;
            return $"{totalHours}h {remainingMinutes}m";
        }

        private static string BuildInitials(string firstName, string lastName)
        {
            var firstInitial = string.IsNullOrWhiteSpace(firstName)
                ? 'A'
                : char.ToUpperInvariant(firstName.Trim()[0]);
            var lastInitial = string.IsNullOrWhiteSpace(lastName)
                ? 'U'
                : char.ToUpperInvariant(lastName.Trim()[0]);

            return $"{firstInitial}{lastInitial}";
        }

        private string GetWorkerCurrentState(TimeSpan? inTime, TimeSpan? outTime, string status)
        {
            if (status == "Leave" || !inTime.HasValue)
            {
                return "On Leave";
            }

            if (!outTime.HasValue)
            {
                return "Working";
            }

            if (outTime.Value > new TimeSpan(16, 30, 0))
            {
                return "Working OT";
            }

            return "Completed";
        }

        private async Task BuildLeaveAgentDashboardViewModelAsync(User currentUser, DateTime today, DashboardViewModel model)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            model.OTCurrentMonthStart = monthStart;
            model.OTCurrentMonthEnd = monthEnd;
            model.OTCurrentMonthLabel = today.ToString("MMMM yyyy");
            model.IsLeaveAgent = true;
            model.IsGM = false;

            // Get users in scope based on role
            var userScope = _context.Users
                .AsNoTracking()
                .Include(u => u.Section)
                .Where(u => u.IsActive && (u.Role.Name == RoleNames.Worker || u.Role.Name == "Worker"));

            if (currentUser.SectionId.HasValue && currentUser.SectionId.Value > 0)
            {
                userScope = userScope.Where(u => u.SectionId == currentUser.SectionId.Value);
            }

            var employees = await userScope.ToListAsync();

            // Calculate OT data for each employee
            var employeeData = new List<OTEmployeeViewModel>();
            var totalOTHours = 0m;

            foreach (var employee in employees)
            {
                var otRecords = await _context.Attendances
                    .AsNoTracking()
                    .Where(a => a.UserId == employee.Id
                        && a.AttendanceDate >= monthStart
                        && a.AttendanceDate <= monthEnd
                        && a.OvertimeMinutes > 0)
                    .ToListAsync();

                var totalOTMinutes = otRecords.Sum(a => a.OvertimeMinutes);
                totalOTHours += totalOTMinutes / 60m;

                // Calculate OT allocation percentage (assuming 160 working hours per month as baseline)
                var baselineHours = 160m;
                var otAllocationPercentage = baselineHours > 0
                    ? ((totalOTMinutes / 60m) / baselineHours) * 100m
                    : 0m;

                var status = otAllocationPercentage switch
                {
                    <= 75 => "Below 75%",
                    <= 80 => "75-80%",
                    <= 95 => "80-95%",
                    <= 100 => "95-100%",
                    _ => "Above 100%"
                };

                employeeData.Add(new OTEmployeeViewModel
                {
                    Id = employee.Id,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName,
                    ServiceId = employee.ServiceId,
                    Department = employee.Section?.Name ?? "Not Assigned",
                    OTAllocationPercentage = Math.Round(otAllocationPercentage, 2),
                    TotalOTHours = Math.Round(totalOTMinutes / 60m, 2),
                    Status = status,
                    Initials = BuildInitials(employee.FirstName, employee.LastName)
                });
            }

            model.OTEmployees = employeeData;

            // Generate OT calendar data
            model.OTCalendarDays = GenerateOTCalendarData(monthStart, employees);

            // Calculate OT summary
            var summary = new OTSummaryViewModel
            {
                TotalEmployees = employees.Count,
                EmployeesAbove100 = employeeData.Count(e => e.OTAllocationPercentage > 100),
                Employees95to100 = employeeData.Count(e => e.OTAllocationPercentage >= 95 && e.OTAllocationPercentage <= 100),
                Employees80to95 = employeeData.Count(e => e.OTAllocationPercentage >= 80 && e.OTAllocationPercentage < 95),
                EmployeesBelow80 = employeeData.Count(e => e.OTAllocationPercentage < 80),
                AverageOTAllocation = employees.Any() ? employeeData.Average(e => e.OTAllocationPercentage) : 0,
                TotalOTHours = totalOTHours
            };
            model.OTSummary = summary;

            // Generate OT alerts
            var alerts = new List<OTAlertViewModel>();
            foreach (var emp in employeeData.Where(e => e.OTAllocationPercentage > 100))
            {
                alerts.Add(new OTAlertViewModel
                {
                    EmployeeId = emp.Id,
                    EmployeeName = $"{emp.FirstName} {emp.LastName}",
                    Department = emp.Department,
                    Reason = $"OT allocation exceeds 100% ({emp.OTAllocationPercentage}%)",
                    AlertType = "danger"
                });
            }
            model.OTAlerts = alerts;

            // Today's OT status
            var todayAttendance = await _context.Attendances
                .AsNoTracking()
                .Where(a => a.AttendanceDate == today)
                .ToListAsync();

            model.TodayOTStatus = new TodayOTStatusViewModel
            {
                Date = today,
                EmployeesOnLeave = todayAttendance.Count(a => a.Status == AttendanceStatus.Leave),
                EmployeesWorking = todayAttendance.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late),
                EmployeesScheduledOT = todayAttendance.Count(a => a.OvertimeMinutes > 0),
                EmployeesAbove100 = summary.EmployeesAbove100
            };

            // Department OT data for chart
            var departmentOTData = employeeData
                .GroupBy(e => e.Department)
                .Select(g => new
                {
                    Department = g.Key,
                    AvgOTAllocation = g.Average(e => e.OTAllocationPercentage)
                })
                .OrderBy(g => g.Department)
                .ToList();

            model.DepartmentOTLabels = departmentOTData.Select(d => d.Department).ToList();
            model.DepartmentOTValues = departmentOTData.Select(d => d.AvgOTAllocation).ToList();

            // Daily OT data for chart
            var dailyOTData = await _context.Attendances
                .AsNoTracking()
                .Where(a => a.AttendanceDate >= monthStart && a.AttendanceDate <= monthEnd && a.OvertimeMinutes > 0)
                .GroupBy(a => a.AttendanceDate)
                .Select(g => new
                {
                    Date = g.Key,
                    TotalOTHours = g.Sum(a => a.OvertimeMinutes) / 60m
                })
                .OrderBy(g => g.Date)
                .ToListAsync();

            model.DailyOTLabels = dailyOTData.Select(d => d.Date.ToString("dd MMM")).ToList();
            model.DailyOTValues = dailyOTData.Select(d => d.TotalOTHours).ToList();

            // OT distribution for doughnut chart
            model.OTDistributionLabels = new List<string> { "Below 80%", "80-95%", "95-100%", "Above 100%" };
            model.OTDistributionValues = new List<int>
            {
                summary.EmployeesBelow80,
                summary.Employees80to95,
                summary.Employees95to100,
                summary.EmployeesAbove100
            };
        }

        private async Task BuildOTDashboardViewModelAsync(User currentUser, DateTime today, DashboardViewModel model)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            model.OTCurrentMonthStart = monthStart;
            model.OTCurrentMonthEnd = monthEnd;
            model.OTCurrentMonthLabel = today.ToString("MMMM yyyy");

            // Get users in scope based on role
            var userScope = _context.Users
                .AsNoTracking()
                .Include(u => u.Section)
                .Where(u => u.IsActive && (u.Role.Name == RoleNames.Worker || u.Role.Name == "Worker"));

            if (currentUser.SectionId.HasValue && currentUser.SectionId.Value > 0)
            {
                userScope = userScope.Where(u => u.SectionId == currentUser.SectionId.Value);
            }

            var employees = await userScope.ToListAsync();

            // Calculate OT data for each employee
            var employeeData = new List<OTEmployeeViewModel>();
            var totalOTHours = 0m;

            foreach (var employee in employees)
            {
                var otRecords = await _context.Attendances
                    .AsNoTracking()
                    .Where(a => a.UserId == employee.Id
                        && a.AttendanceDate >= monthStart
                        && a.AttendanceDate <= monthEnd
                        && a.OvertimeMinutes > 0)
                    .ToListAsync();

                var totalOTMinutes = otRecords.Sum(a => a.OvertimeMinutes);
                totalOTHours += totalOTMinutes / 60m;

                // Calculate OT allocation percentage (assuming 160 working hours per month as baseline)
                var baselineHours = 160m;
                var otAllocationPercentage = baselineHours > 0
                    ? ((totalOTMinutes / 60m) / baselineHours) * 100m
                    : 0m;

                var status = otAllocationPercentage switch
                {
                    <= 75 => "Below 75%",
                    <= 80 => "75-80%",
                    <= 95 => "80-95%",
                    <= 100 => "95-100%",
                    _ => "Above 100%"
                };

                employeeData.Add(new OTEmployeeViewModel
                {
                    Id = employee.Id,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName,
                    ServiceId = employee.ServiceId,
                    Department = employee.Section?.Name ?? "Not Assigned",
                    OTAllocationPercentage = Math.Round(otAllocationPercentage, 2),
                    TotalOTHours = Math.Round(totalOTMinutes / 60m, 2),
                    Status = status,
                    Initials = BuildInitials(employee.FirstName, employee.LastName)
                });
            }

            model.OTEmployees = employeeData;

            // Generate OT calendar data
            model.OTCalendarDays = GenerateOTCalendarData(monthStart, employees);

            // Calculate OT summary
            var summary = new OTSummaryViewModel
            {
                TotalEmployees = employees.Count,
                EmployeesAbove100 = employeeData.Count(e => e.OTAllocationPercentage > 100),
                Employees95to100 = employeeData.Count(e => e.OTAllocationPercentage >= 95 && e.OTAllocationPercentage <= 100),
                Employees80to95 = employeeData.Count(e => e.OTAllocationPercentage >= 80 && e.OTAllocationPercentage < 95),
                EmployeesBelow80 = employeeData.Count(e => e.OTAllocationPercentage < 80),
                AverageOTAllocation = employees.Any() ? employeeData.Average(e => e.OTAllocationPercentage) : 0,
                TotalOTHours = totalOTHours
            };
            model.OTSummary = summary;

            // Generate OT alerts
            var alerts = new List<OTAlertViewModel>();
            foreach (var emp in employeeData.Where(e => e.OTAllocationPercentage > 100))
            {
                alerts.Add(new OTAlertViewModel
                {
                    EmployeeId = emp.Id,
                    EmployeeName = $"{emp.FirstName} {emp.LastName}",
                    Department = emp.Department,
                    Reason = $"OT allocation exceeds 100% ({emp.OTAllocationPercentage}%)",
                    AlertType = "danger"
                });
            }
            model.OTAlerts = alerts;

            // Today's OT status
            var todayAttendance = await _context.Attendances
                .AsNoTracking()
                .Where(a => a.AttendanceDate == today)
                .ToListAsync();

            model.TodayOTStatus = new TodayOTStatusViewModel
            {
                Date = today,
                EmployeesOnLeave = todayAttendance.Count(a => a.Status == AttendanceStatus.Leave),
                EmployeesWorking = todayAttendance.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late),
                EmployeesScheduledOT = todayAttendance.Count(a => a.OvertimeMinutes > 0),
                EmployeesAbove100 = summary.EmployeesAbove100
            };

            // Department OT data for chart
            var departmentOTData = employeeData
                .GroupBy(e => e.Department)
                .Select(g => new
                {
                    Department = g.Key,
                    AvgOTAllocation = g.Average(e => e.OTAllocationPercentage)
                })
                .OrderBy(g => g.Department)
                .ToList();

            model.DepartmentOTLabels = departmentOTData.Select(d => d.Department).ToList();
            model.DepartmentOTValues = departmentOTData.Select(d => d.AvgOTAllocation).ToList();

            // Daily OT data for chart
            var dailyOTData = await _context.Attendances
                .AsNoTracking()
                .Where(a => a.AttendanceDate >= monthStart && a.AttendanceDate <= monthEnd && a.OvertimeMinutes > 0)
                .GroupBy(a => a.AttendanceDate)
                .Select(g => new
                {
                    Date = g.Key,
                    TotalOTHours = g.Sum(a => a.OvertimeMinutes) / 60m
                })
                .OrderBy(g => g.Date)
                .ToListAsync();

            model.DailyOTLabels = dailyOTData.Select(d => d.Date.ToString("dd MMM")).ToList();
            model.DailyOTValues = dailyOTData.Select(d => d.TotalOTHours).ToList();

            // OT distribution for doughnut chart
            model.OTDistributionLabels = new List<string> { "Below 80%", "80-95%", "95-100%", "Above 100%" };
            model.OTDistributionValues = new List<int>
            {
                summary.EmployeesBelow80,
                summary.Employees80to95,
                summary.Employees95to100,
                summary.EmployeesAbove100
            };
        }

        private List<OTCalendarDayViewModel> GenerateOTCalendarData(DateTime monthStart, List<User> employees)
        {
            var calendarDays = new List<OTCalendarDayViewModel>();
            var firstDayOfMonth = monthStart;
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
            var startDate = firstDayOfMonth.AddDays(-(int)firstDayOfMonth.DayOfWeek); // Start from Sunday
            var today = DateTime.Today;

            for (var date = startDate; date <= startDate.AddDays(41); date = date.AddDays(1))
            {
                var isCurrentMonth = date.Month == monthStart.Month && date.Year == monthStart.Year;
                var isToday = date.Date == today.Date;

                if (isCurrentMonth)
                {
                    var employeeIds = employees.Select(e => e.Id).ToList();
                    var otRecords = _context.Attendances
                        .AsNoTracking()
                        .Where(a => employeeIds.Contains(a.UserId)
                            && a.AttendanceDate == date
                            && a.OvertimeMinutes > 0)
                        .ToList();

                    var employeesOnOT = otRecords.Select(a => a.UserId).Distinct().Count();
                    var totalEmployees = employees.Count;
                    var avgOTAllocation = totalEmployees > 0
                        ? (employeesOnOT * 100m / totalEmployees)
                        : 0m;

                    var otStatus = avgOTAllocation switch
                    {
                        <= 75 => "below75",
                        <= 80 => "75-80",
                        <= 95 => "80-95",
                        <= 100 => "95-100",
                        _ => "above100"
                    };

                    calendarDays.Add(new OTCalendarDayViewModel
                    {
                        Day = date.Day,
                        Month = date.Month,
                        Year = date.Year,
                        IsCurrentMonth = isCurrentMonth,
                        IsToday = isToday,
                        EmployeesOnOT = employeesOnOT,
                        TotalEmployees = totalEmployees,
                        AverageOTAllocation = avgOTAllocation,
                        OTStatus = otStatus
                    });
                }
                else
                {
                    calendarDays.Add(new OTCalendarDayViewModel
                    {
                        Day = date.Day,
                        Month = date.Month,
                        Year = date.Year,
                        IsCurrentMonth = isCurrentMonth,
                        IsToday = isToday,
                        EmployeesOnOT = 0,
                        TotalEmployees = 0,
                        AverageOTAllocation = 0,
                        OTStatus = "below75"
                    });
                }
            }

            return calendarDays;
        }
    }
}
