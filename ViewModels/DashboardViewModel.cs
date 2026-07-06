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
}
