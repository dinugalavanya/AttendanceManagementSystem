namespace AttendanceManagementSystem.ViewModels
{
    public class DashboardViewModel
    {
        public DateTime SelectedDate { get; set; } = DateTime.Today;
        
        public int TotalUsers { get; set; }
        public int TodayAttendance { get; set; }
        public int PresentCount { get; set; }
        public int LateCount { get; set; }
        public int AbsentCount { get; set; }
        public int OnLeaveCount { get; set; }
        public int OvertimeMinutesToday { get; set; }
        public int OvertimeMinutesMonth { get; set; }

        public int WorkingDaysToDate { get; set; }
        public int AttendedDaysToDate { get; set; }
        public int MonthlyPresentCount { get; set; }
        public int MonthlyAbsentCount { get; set; }
        public int MonthlyLateCount { get; set; }
        public decimal AttendanceTargetPercent { get; set; }

        public string ScopeTitle { get; set; } = string.Empty;
        public bool IsSuperAdmin { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsWorker { get; set; }
        public bool IsLeaveAgent { get; set; }
        public bool IsGM { get; set; }

        // OT Dashboard properties
        public DateTime OTCurrentMonthStart { get; set; }
        public DateTime OTCurrentMonthEnd { get; set; }
        public string OTCurrentMonthLabel { get; set; } = string.Empty;
        public List<OTCalendarDayViewModel> OTCalendarDays { get; set; } = new();
        public List<OTEmployeeViewModel> OTEmployees { get; set; } = new();
        public OTSummaryViewModel OTSummary { get; set; } = new();
        public List<OTAlertViewModel> OTAlerts { get; set; } = new();
        public TodayOTStatusViewModel TodayOTStatus { get; set; } = new();
        public List<string> DepartmentOTLabels { get; set; } = new();
        public List<decimal> DepartmentOTValues { get; set; } = new();
        public List<string> DailyOTLabels { get; set; } = new();
        public List<decimal> DailyOTValues { get; set; } = new();
        public List<string> OTDistributionLabels { get; set; } = new();
        public List<int> OTDistributionValues { get; set; } = new();

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
        public string? SearchServiceId { get; set; }
        public string? WorkerSearchMessage { get; set; }
        public WorkerHistorySearchViewModel? WorkerHistorySearchResult { get; set; }
        public SelectedDateAttendanceViewModel? SelectedDateAttendance { get; set; }
        public WorkerOtDashboardViewModel? WorkerOtDashboard { get; set; }
    }

    public class SectionSnapshotItem
    {
        public string SectionName { get; set; } = string.Empty;
        public int PresentCount { get; set; }
        public int LateCount { get; set; }
        public int AbsentCount { get; set; }
    }

    public class SelectedDateAttendanceViewModel
    {
        public string WorkerName { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public DateTime SelectedDate { get; set; }
        public string InTime { get; set; } = string.Empty;
        public string OutTime { get; set; } = string.Empty;
        public string WorkedHours { get; set; } = string.Empty;
        public string OTHours { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string LateBy { get; set; } = string.Empty;
        public string CurrentState { get; set; } = string.Empty;
        public bool HasRecord { get; set; }
    }

    public class RecentAttendanceItem
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string? SectionName { get; set; }
        public string Status { get; set; } = string.Empty;
        public TimeSpan? InTime { get; set; }
        public TimeSpan? OutTime { get; set; }
    }

    public class WorkerOtDashboardViewModel
    {
        public DateTime MonthStartDate { get; set; }
        public DateTime SearchDate { get; set; }
        public string Initials { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;

        public string SearchedDateOtInTime { get; set; } = "-";
        public string SearchedDateOtOutTime { get; set; } = "-";
        public string SearchedDateOtDuration { get; set; } = "-";

        public int TotalOtMinutes { get; set; }
        public int OtDays { get; set; }
        public decimal AverageOtHoursPerDay { get; set; }

        public int TotalRecords { get; set; }
        public string TotalOtDurationDisplay { get; set; } = "0h 0m";

        public List<WorkerOtTrendPointViewModel> TrendPoints { get; set; } = new();
        public List<WorkerOtRecordRowViewModel> RecentOtRecords { get; set; } = new();
    }

    public class WorkerOtTrendPointViewModel
    {
        public string DateLabel { get; set; } = string.Empty;
        public decimal OtHours { get; set; }
    }

    public class WorkerOtRecordRowViewModel
    {
        public DateTime Date { get; set; }
        public string OtInTime { get; set; } = "-";
        public string OtOutTime { get; set; } = "-";
        public string OtDuration { get; set; } = "-";
    }

    // OT Dashboard ViewModel classes
    public class OTCalendarDayViewModel
    {
        public int Day { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public bool IsCurrentMonth { get; set; }
        public bool IsToday { get; set; }
        public int EmployeesOnOT { get; set; }
        public int TotalEmployees { get; set; }
        public decimal AverageOTAllocation { get; set; }
        public string OTStatus { get; set; } = "below75"; // below75, 75-80, 80-95, 95-100, above100
    }

    public class OTDailyTaskViewModel
    {
        public DateTime Date { get; set; }
        public decimal OTHours { get; set; }
        public string TaskDescription { get; set; } = string.Empty;
    }

    public class OTEmployeeViewModel
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public decimal OTAllocationPercentage { get; set; }
        public decimal TotalOTHours { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public List<OTDailyTaskViewModel> DailyTasks { get; set; } = new();
    }

    public class OTSummaryViewModel
    {
        public int TotalEmployees { get; set; }
        public int EmployeesAbove100 { get; set; }
        public int Employees95to100 { get; set; }
        public int Employees80to95 { get; set; }
        public int EmployeesBelow80 { get; set; }
        public decimal AverageOTAllocation { get; set; }
        public decimal TotalOTHours { get; set; }
    }

    public class OTAlertViewModel
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string AlertType { get; set; } = "danger"; // danger, warning
    }

    public class TodayOTStatusViewModel
    {
        public DateTime Date { get; set; }
        public int EmployeesOnLeave { get; set; }
        public int EmployeesWorking { get; set; }
        public int EmployeesScheduledOT { get; set; }
        public int EmployeesAbove100 { get; set; }
    }
}
