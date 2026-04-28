using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Models;
using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.ViewModels;
using AttendanceManagementSystem.Services;
using System.Security.Claims;

namespace AttendanceManagementSystem.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AttendanceCalculationService _calculationService;

        public DashboardController(ApplicationDbContext context, AttendanceCalculationService calculationService)
        {
            _context = context;
            _calculationService = calculationService;
            
            // Test calculation examples to verify logic is correct
            Console.WriteLine("[ATTENDANCE CALCULATION VALIDATION]");
            _calculationService.ValidateCalculationExamples();
            Console.WriteLine("[ATTENDANCE CALCULATION VALIDATION COMPLETE]");
        }

        public async Task<IActionResult> Index(string? serviceId, DateTime? selectedDate)
        {
            Console.WriteLine($"[WORKER SEARCH] serviceId received: {serviceId}, selectedDate: {selectedDate?.ToString("yyyy-MM-dd")}");

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

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEndExclusive = monthStart.AddMonths(1);

            var isSuperAdmin = currentUser.Role.Name == RoleNames.SuperAdmin;
            var isAdmin = currentUser.Role.Name == RoleNames.Admin;
            var isWorker = currentUser.Role.Name == RoleNames.Worker;

            var userScope = _context.Users.AsNoTracking().Where(u => u.IsActive);
            var attendanceScope = _context.Attendances.AsNoTracking().AsQueryable();

            if (isWorker)
            {
                userScope = userScope.Where(u => u.Id == currentUser.Id);
                attendanceScope = attendanceScope.Where(a => a.UserId == currentUser.Id);
            }
            else if (isAdmin)
            {
                if (!currentUser.SectionId.HasValue)
                {
                    var adminViewModel = new AdminDashboardViewModel
                    {
                        HasSection = false
                    };
                    return View("AdminDashboard", adminViewModel);
                }

                var sectionId = currentUser.SectionId.Value;
                var sectionName = currentUser.Section?.Name ?? string.Empty;
                userScope = userScope.Where(u => u.SectionId == sectionId);
                attendanceScope = attendanceScope.Where(a => a.User.SectionId == sectionId);

                var sectionTotalUsers = await userScope
                    .AsNoTracking()
                    .CountAsync();

                // Get today's attendance for section workers
                var todayAttendances = await attendanceScope
                    .Where(a => a.AttendanceDate >= today && a.AttendanceDate < tomorrow)
                    .Include(a => a.User)
                    .ToListAsync();

                // Use AttendanceCalculationService to recalculate all attendance records with standardized rules
                var todayCalculated = todayAttendances.Select(a => _calculationService.CalculateAttendance(
                    a.AttendanceDate, a.InTime, a.OutTime, a.Status)).ToList();

                var presentToday = todayCalculated.Count(c => c.Status == "On Time");
                var lateToday = todayCalculated.Count(c => c.Status == "Late");
                var leaveToday = todayCalculated.Count(c => c.Status == "Leave");
                var workingOtToday = todayCalculated.Count(c => c.CurrentState == "Working OT");

                // Calculate OT hours for today using standardized rules (only after 8 hours)
                var todayOtMinutes = todayCalculated.Sum(c => c.OvertimeMinutes);
                var todayOtHours = todayOtMinutes / 60m;

                // Calculate total OT hours this month using standardized rules
                var monthlyAttendances = await attendanceScope
                    .Where(a => a.AttendanceDate >= monthStart && a.AttendanceDate < monthEndExclusive)
                    .ToListAsync();

                var monthlyCalculated = monthlyAttendances.Select(a => _calculationService.CalculateAttendance(
                    a.AttendanceDate, a.InTime, a.OutTime, a.Status)).ToList();

                var monthlyOtMinutes = monthlyCalculated.Sum(c => c.OvertimeMinutes);
                var monthlyOtHours = monthlyOtMinutes / 60m;

                // Prepare worker attendance rows for table using standardized calculations
                var todayWorkers = todayCalculated
                    .Select((c, index) => new WorkerAttendanceRow
                    {
                        WorkerName = todayAttendances[index].User?.FirstName + " " + todayAttendances[index].User?.LastName ?? "Unknown",
                        LoginTimeDisplay = c.InTimeText,
                        LogoutTimeDisplay = c.OutTimeText,
                        Status = c.Status,
                        LateByDisplay = c.LateByText != "-" ? c.LateByText : "On Time",
                        OtHoursDisplay = c.OvertimeHoursText != "-" ? c.OvertimeHoursText : "0h",
                        CurrentState = c.CurrentState
                    })
                    .OrderBy(a => a.WorkerName)
                    .ToList();

                // Prepare work hours pie chart using standardized calculations
                var workHoursPieChart = todayCalculated
                    .Where(c => c.TotalWorkedMinutes > 0)
                    .Select((c, index) => new 
                    {
                        WorkerName = todayAttendances[index].User?.FirstName + " " + todayAttendances[index].User?.LastName ?? "Unknown",
                        TotalWorkedMinutes = c.TotalWorkedMinutes
                    })
                    .GroupBy(x => x.WorkerName)
                    .Select(g => new PieChartItem
                    {
                        Label = g.Key,
                        Hours = (decimal)g.Sum(x => x.TotalWorkedMinutes) / 60m
                    })
                    .ToList();

                // Prepare OT workers list using standardized calculations (only after 8 hours)
                var otWorkers = todayCalculated
                    .Where(c => c.OvertimeMinutes > 0)
                    .Select((c, index) => new OtWorkerItem
                    {
                        WorkerName = todayAttendances[index].User?.FirstName + " " + todayAttendances[index].User?.LastName ?? "Unknown",
                        OtHours = (decimal)c.OvertimeMinutes / 60m,
                        LogoutTimeDisplay = c.OutTimeText
                    })
                    .OrderByDescending(a => a.OtHours)
                    .ToList();

                // Prepare calendar summary using standardized calculations
                var monthlyAttendancesForCalendar = await attendanceScope
                    .Where(a => a.AttendanceDate >= monthStart && a.AttendanceDate < monthEndExclusive)
                    .ToListAsync();

                var monthlyCalculatedForCalendar = monthlyAttendancesForCalendar.Select(a => _calculationService.CalculateAttendance(
                    a.AttendanceDate, a.InTime, a.OutTime, a.Status)).ToList();

                var calendarSummary = monthlyCalculatedForCalendar
                    .GroupBy(c => c.AttendanceDate.Date)
                    .Select(g => new DailyAttendanceSummary
                    {
                        Date = g.Key,
                        PresentCount = g.Count(c => c.Status == "On Time"),
                        LateCount = g.Count(c => c.Status == "Late"),
                        LeaveCount = g.Count(c => c.Status == "Leave"),
                        TotalOtHours = (decimal)g.Sum(c => c.OvertimeMinutes) / 60m
                    })
                    .OrderBy(g => g.Date)
                    .ToList();

                // Prepare last 2 weeks calendar data using standardized calculations (last 14 days only)
                var twoWeeksAgo = today.AddDays(-13);
                var lastTwoWeeksAttendances = await attendanceScope
                    .Where(a => a.AttendanceDate >= twoWeeksAgo && a.AttendanceDate < tomorrow)
                    .ToListAsync();

                var lastTwoWeeksCalculated = lastTwoWeeksAttendances.Select(a => _calculationService.CalculateAttendance(
                    a.AttendanceDate, a.InTime, a.OutTime, a.Status)).ToList();

                var lastTwoWeeksCalendarData = lastTwoWeeksCalculated
                    .GroupBy(c => c.AttendanceDate.Date)
                    .Select(g => new DailyAttendanceSummary
                    {
                        Date = g.Key,
                        PresentCount = g.Count(c => c.Status == "On Time"),
                        LateCount = g.Count(c => c.Status == "Late"),
                        LeaveCount = g.Count(c => c.Status == "Leave"),
                        TotalOtHours = (decimal)g.Sum(c => c.OvertimeMinutes) / 60m
                    })
                    .OrderByDescending(g => g.Date)
                    .Take(14)
                    .ToList();

                // Load active sections for the Add Employee modal
                ViewBag.Sections = await _context.Sections
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                var adminDashboardViewModel = new AdminDashboardViewModel
                {
                    SectionName = sectionName,
                    TotalWorkers = sectionTotalUsers,
                    PresentToday = presentToday,
                    LateToday = lateToday,
                    LeaveToday = leaveToday,
                    WorkingOtToday = workingOtToday,
                    TotalOtHoursThisMonth = (double)monthlyOtHours,
                    TodayWorkers = todayWorkers,
                    WorkHoursPieChart = workHoursPieChart,
                    OtWorkers = otWorkers,
                    CalendarSummary = calendarSummary,
                    LastTwoWeeksCalendarData = lastTwoWeeksCalendarData,
                    HasSection = true,
                    OnTimeCount = presentToday,
                    LateCount = lateToday,
                    LeaveCount = leaveToday,
                    OtCount = workingOtToday
                };

                // Add search logic here
                if (!string.IsNullOrWhiteSpace(serviceId))
                {
                    var normalizedServiceId = serviceId.Trim().ToUpper();
                    adminDashboardViewModel.SearchServiceId = normalizedServiceId;

                    Console.WriteLine($"[WORKER SEARCH] normalized serviceId: {normalizedServiceId}");

                    var worker = await _context.Users
                        .Include(u => u.Section)
                        .FirstOrDefaultAsync(u => u.ServiceId != null && u.ServiceId.ToUpper() == normalizedServiceId);

                    if (worker == null)
                    {
                        adminDashboardViewModel.WorkerSearchMessage = $"Worker with Service ID {normalizedServiceId} not found.";
                        Console.WriteLine("[WORKER SEARCH] worker not found");
                    }
                    else
                    {
                        Console.WriteLine($"[WORKER SEARCH] worker found: {worker.Id} {worker.FullName}");

                        // Check permissions based on user role
                        if (isAdmin)
                        {
                            // Admin role: check section permissions
                            if (!currentUser.SectionId.HasValue)
                            {
                                adminDashboardViewModel.WorkerSearchMessage = "You are not assigned to a section.";
                                Console.WriteLine("[WORKER SEARCH] Admin not assigned to section");
                            }
                            else if (worker.SectionId != currentUser.SectionId)
                            {
                                adminDashboardViewModel.WorkerSearchMessage = "You do not have permission to view workers outside your section.";
                                Console.WriteLine("[WORKER SEARCH] Admin trying to access worker outside section");
                            }
                            else
                            {
                                // Admin has permission to view worker in their section
                                await LoadWorkerHistory(adminDashboardViewModel, worker);
                            }
                        }
                        else if (isSuperAdmin)
                        {
                            // SuperAdmin role: can view any worker
                            await LoadWorkerHistory(adminDashboardViewModel, worker);
                        }
                        else
                        {
                            // Worker role: should not see search functionality
                            adminDashboardViewModel.WorkerSearchMessage = "Worker role does not have search permissions.";
                            Console.WriteLine("[WORKER SEARCH] Worker role attempting search");
                        }
                    }
                }

                // Helper method to load worker history
                async Task LoadWorkerHistory(AdminDashboardViewModel model, User targetWorker)
                {
                    var history = await _context.Attendances
                        .Where(a => a.UserId == targetWorker.Id)
                        .OrderByDescending(a => a.AttendanceDate)
                        .Take(30)
                        .ToListAsync();

                    Console.WriteLine($"[WORKER SEARCH] history count: {history.Count}");

                    // Set worker search mode
                    model.IsWorkerSearchMode = true;

                    // Calculate worker statistics
                    var totalWorkedHours = Math.Round(history.Sum(a => a.TotalWorkedMinutes) / 60m, 2);
                    var totalOTHours = Math.Round(history.Sum(a => a.OvertimeMinutes) / 60m, 2);
                    var otDaysCount = history.Count(a => a.OvertimeMinutes > 0);
                    var leaveDaysCount = history.Count(a => a.Status == "Leave");
                    var lateDaysCount = history.Count(a => a.Status == "Late");
                    var onTimeDaysCount = history.Count(a => a.Status == "Present");
                    
                    // Get latest status and attendance
                    var latestStatus = history.FirstOrDefault()?.Status ?? "No Record";
                    var latestAttendanceRecord = history.FirstOrDefault();
                    WorkerAttendanceHistoryRowViewModel? latestAttendance = null;
                    
                    if (latestAttendanceRecord != null)
                    {
                        latestAttendance = new WorkerAttendanceHistoryRowViewModel
                        {
                            Date = latestAttendanceRecord.AttendanceDate,
                            InTime = latestAttendanceRecord.InTime.HasValue ? latestAttendanceRecord.InTime.Value.ToString(@"hh\:mm") : "-",
                            OutTime = latestAttendanceRecord.OutTime.HasValue ? latestAttendanceRecord.OutTime.Value.ToString(@"hh\:mm") : "-",
                            WorkedHours = latestAttendanceRecord.TotalWorkedDisplay,
                            OTHours = latestAttendanceRecord.OvertimeMinutes > 0 ? latestAttendanceRecord.OvertimeDisplay : "-",
                            OTHoursValue = latestAttendanceRecord.OvertimeMinutes / 60m,
                            Status = latestAttendanceRecord.Status
                        };
                    }

                    // Build history list with OTHoursValue
                    var historyList = history.Select(a => new WorkerAttendanceHistoryRowViewModel
                    {
                        Date = a.AttendanceDate,
                        InTime = a.InTime.HasValue ? a.InTime.Value.ToString(@"hh\:mm") : "-",
                        OutTime = a.OutTime.HasValue ? a.OutTime.Value.ToString(@"hh\:mm") : "-",
                        WorkedHours = a.TotalWorkedDisplay,
                        OTHours = a.OvertimeMinutes > 0 ? a.OvertimeDisplay : "-",
                        OTHoursValue = a.OvertimeMinutes / 60m,
                        Status = a.Status
                    }).ToList();

                    // Build OT history list (only records with overtime)
                    var otHistoryList = history.Where(a => a.OvertimeMinutes > 0)
                        .Select(a => new WorkerAttendanceHistoryRowViewModel
                        {
                            Date = a.AttendanceDate,
                            InTime = a.InTime.HasValue ? a.InTime.Value.ToString(@"hh\:mm") : "-",
                            OutTime = a.OutTime.HasValue ? a.OutTime.Value.ToString(@"hh\:mm") : "-",
                            WorkedHours = a.TotalWorkedDisplay,
                            OTHours = a.OvertimeDisplay,
                            OTHoursValue = a.OvertimeMinutes / 60m,
                            Status = a.Status
                        }).ToList();

                    // Calculate chart data
                    var orderedHistory = history.OrderBy(a => a.AttendanceDate).ToList();
                    var chartLabels = orderedHistory.Select(a => a.AttendanceDate.ToString("dd MMM")).ToList();
                    var workedHoursChartData = orderedHistory.Select(a => Math.Round(a.TotalWorkedMinutes / 60m, 2)).ToList();
                    var otHoursChartData = orderedHistory.Select(a => Math.Round(a.OvertimeMinutes / 60m, 2)).ToList();

                    model.WorkerHistorySearchResult = new WorkerHistorySearchViewModel
                    {
                        WorkerId = targetWorker.Id,
                        ServiceId = targetWorker.ServiceId ?? "-",
                        FullName = targetWorker.FullName,
                        Email = targetWorker.Email,
                        SectionName = targetWorker.Section != null ? targetWorker.Section.Name : "Unassigned",
                        LatestStatus = latestStatus,
                        TotalWorkedHours = totalWorkedHours,
                        TotalOTHours = totalOTHours,
                        OTDaysCount = otDaysCount,
                        LeaveDaysCount = leaveDaysCount,
                        LateDaysCount = lateDaysCount,
                        OnTimeDaysCount = onTimeDaysCount,
                        LatestAttendance = latestAttendance,
                        History = historyList,
                        OTHistory = otHistoryList,
                        ChartLabels = chartLabels,
                        WorkedHoursChartData = workedHoursChartData,
                        OTHoursChartData = otHoursChartData
                    };

                    if (!history.Any())
                    {
                        model.WorkerSearchMessage = "Worker found, but no attendance history exists.";
                    }
                }

                return View("AdminDashboard", adminDashboardViewModel);
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
                var worker = await _context.Users
                    .Include(u => u.Section)
                    .FirstOrDefaultAsync(u => u.ServiceId == serviceId);

                if (worker == null)
                {
                    workerSearchMessage = $"Worker with Service ID {serviceId} not found.";
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
                
                var attendanceOnDate = await _context.Attendances
                    .Where(a => a.UserId == workerHistorySearchResult.WorkerId && a.AttendanceDate == selectedDate.Value)
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
                    : isAdmin
                        ? $"{currentUser.Section?.Name ?? "Section"} attendance"
                        : "My attendance overview",
                IsSuperAdmin = isSuperAdmin,
                IsAdmin = isAdmin,
                IsWorker = isWorker,
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

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetWorkerAttendanceByDate(string serviceId, DateTime date)
        {
            Console.WriteLine($"[WORKER DATE SEARCH] serviceId: {serviceId}, date: {date:yyyy-MM-dd}");

            if (string.IsNullOrWhiteSpace(serviceId))
            {
                return Json(new { success = false, message = "Service ID is required." });
            }

            // Step 1: Find worker from Users table by ServiceId
            var worker = await _context.Users
                .Include(u => u.Section)
                .FirstOrDefaultAsync(u => u.ServiceId == serviceId);

            if (worker == null)
            {
                Console.WriteLine($"[WORKER DATE SEARCH] Worker with ServiceId {serviceId} not found");
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
                    message = $"No attendance record found for {serviceId} on {targetDate:MMM dd, yyyy}."
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
    }
}
