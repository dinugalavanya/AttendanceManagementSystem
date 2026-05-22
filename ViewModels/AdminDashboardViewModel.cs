using System;
using System.Collections.Generic;
using AttendanceManagementSystem.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AttendanceManagementSystem.ViewModels
{
    public class AdminDashboardViewModel
    {
        public string SectionName { get; set; } = string.Empty;
        public int TotalWorkers { get; set; }
        public int PresentToday { get; set; }
        public int LateToday { get; set; }
        public int LeaveToday { get; set; }
        public int WorkingOtToday { get; set; }
        public int CompletedOtToday { get; set; }
        public double TotalOtHoursToday { get; set; }
        public double TotalOtHoursThisMonth { get; set; }
        public List<WorkerAttendanceRow> TodayWorkers { get; set; } = new();
        public List<PieChartItem> WorkHoursPieChart { get; set; } = new();
        public List<OtWorkerItem> OtWorkers { get; set; } = new();
        public List<DailyAttendanceSummary> CalendarSummary { get; set; } = new();
        public List<DailyAttendanceSummary> LastTwoWeeksCalendarData { get; set; } = new();
        public bool HasSection { get; set; }
        public string NoSectionMessage { get; set; } = "No section assigned to this Admin. Contact SuperAdmin.";
        
        // Today Status Breakdown for pie chart
        public int OnTimeCount { get; set; }
        public int LateCount { get; set; }
        public int LeaveCount { get; set; }
        public int OtCount { get; set; }
        
        // Properties for AdminController compatibility
        public int TotalUsers { get; set; }
        public int TotalSections { get; set; }
        public int TodayAttendance { get; set; }
        public int PresentCount { get; set; }
        public int LateCount1 { get; set; }
        public int AbsentCount { get; set; }
        public int OnLeaveCount { get; set; }
        public int OvertimeMinutesToday { get; set; }
        public int OvertimeMinutesMonth { get; set; }
        public int WorkingDaysToDate { get; set; }
        public int AttendedDaysToDate { get; set; }
        public int MonthlyPresentCount { get; set; }
        public int MonthlyLateCount { get; set; }
        public int MonthlyAbsentCount { get; set; }
        public decimal AttendanceTargetPercent { get; set; }
        public string ScopeTitle { get; set; } = string.Empty;
        public bool IsSuperAdmin { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsWorker { get; set; }
        public List<string> TrendLabels { get; set; } = new();
        public List<int> PresentTrend { get; set; } = new();
        public List<int> LateTrend { get; set; } = new();
        public List<int> AbsentTrend { get; set; } = new();
        public List<int> OvertimeTrendHours { get; set; } = new();
        public List<string> DistributionLabels { get; set; } = new();
        public List<int> DistributionValues { get; set; } = new();
        public List<SectionSnapshotItem> SectionSnapshots { get; set; } = new();
        public List<RecentAttendanceItem> RecentAttendance { get; set; } = new();
        public string UserName { get; set; } = string.Empty;
        
        // Worker search functionality
        public bool IsWorkerSearchMode { get; set; }
        public string? SearchServiceId { get; set; }
        public string? WorkerSearchMessage { get; set; }
        public DateTime SingleDate { get; set; } = DateTime.Today;
        public DateTime RangeFromDate { get; set; } = DateTime.Today.AddDays(-7);
        public DateTime RangeToDate { get; set; } = DateTime.Today;
        public bool ShowWorkerResultCards { get; set; }
        public WorkerOtProfileViewModel? WorkerProfile { get; set; }
        public WorkerSingleDateOtDetailsViewModel? SingleDateOtDetails { get; set; }
        public WorkerDateRangeOtSummaryViewModel? DateRangeOtSummary { get; set; }
        public List<DailyOtChartPointViewModel> DateRangeChartData { get; set; } = new();
        public string? SingleDateMessage { get; set; }
        public string? DateRangeMessage { get; set; }
        public CreateWorkerViewModel AddWorker { get; set; } = new();
        public List<SelectListItem> Sections { get; set; } = new();
        public WorkerHistorySearchViewModel? WorkerHistorySearchResult { get; set; }
        public SelectedDateAttendanceViewModel? SelectedDateAttendance { get; set; }
        
        // Properties for Add Employee form
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public int? SectionId { get; set; }
    }

    public class WorkerAttendanceRow
    {
        public string WorkerName { get; set; } = string.Empty;
        public string OTLoginTimeDisplay { get; set; } = string.Empty;
        public string OTLogoutTimeDisplay { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string LateByDisplay { get; set; } = string.Empty;
        public string OtHoursDisplay { get; set; } = string.Empty;
        public string CurrentState { get; set; } = string.Empty;
    }

    public class PieChartItem
    {
        public string Label { get; set; } = string.Empty;
        public decimal Hours { get; set; }
    }

    public class OtWorkerItem
    {
        public string WorkerName { get; set; } = string.Empty;
        public decimal OtHours { get; set; }
        public string OTLoginTimeDisplay { get; set; } = string.Empty;
        public string OTLogoutTimeDisplay { get; set; } = string.Empty;
    }

    public class DailyAttendanceSummary
    {
        public DateTime Date { get; set; }
        public int PresentCount { get; set; }
        public int LateCount { get; set; }
        public int LeaveCount { get; set; }
        public decimal TotalOtHours { get; set; }
    }

    public class WorkerOtProfileViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public string ServiceId { get; set; } = "-";
        public string SectionName { get; set; } = "Unassigned";
        public string Initials { get; set; } = "AU";
    }

    public class WorkerSingleDateOtDetailsViewModel
    {
        public DateTime Date { get; set; }
        public string OvertimeInTime { get; set; } = "-";
        public string OvertimeOutTime { get; set; } = "-";
        public string OvertimeHours { get; set; } = "0h 0m";
    }

    public class WorkerDateRangeOtSummaryViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int TotalDays { get; set; }
        public int TotalOvertimeMinutes { get; set; }
        public string TotalOvertimeDisplay { get; set; } = "0h 0m";
    }

    public class DailyOtChartPointViewModel
    {
        public string DateLabel { get; set; } = string.Empty;
        public decimal OTHours { get; set; }
    }

    }
