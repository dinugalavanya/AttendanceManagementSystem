using System;
using System.Collections.Generic;

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
    }

    public class WorkerAttendanceRow
    {
        public string WorkerName { get; set; } = string.Empty;
        public string LoginTimeDisplay { get; set; } = string.Empty;
        public string LogoutTimeDisplay { get; set; } = string.Empty;
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
        public string LogoutTimeDisplay { get; set; } = string.Empty;
    }

    public class DailyAttendanceSummary
    {
        public DateTime Date { get; set; }
        public int PresentCount { get; set; }
        public int LateCount { get; set; }
        public int LeaveCount { get; set; }
        public decimal TotalOtHours { get; set; }
    }
}
