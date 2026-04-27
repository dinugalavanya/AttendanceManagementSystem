namespace AttendanceManagementSystem.ViewModels
{
    public class DashboardViewModel
    {
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
    }

    public class SectionSnapshotItem
    {
        public string SectionName { get; set; } = string.Empty;
        public int PresentCount { get; set; }
        public int LateCount { get; set; }
        public int AbsentCount { get; set; }
    }

    public class RecentAttendanceItem
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string? SectionName { get; set; }
        public string Status { get; set; } = string.Empty;
        public TimeSpan? InTime { get; set; }
        public TimeSpan? OutTime { get; set; }
    }
}
