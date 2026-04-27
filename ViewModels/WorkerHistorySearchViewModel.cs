namespace AttendanceManagementSystem.ViewModels
{
    public class WorkerHistorySearchViewModel
    {
        public int WorkerId { get; set; }
        public string ServiceId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string LatestStatus { get; set; } = string.Empty;

        public decimal TotalWorkedHours { get; set; }
        public decimal TotalOTHours { get; set; }
        public int OTDaysCount { get; set; }
        public int LeaveDaysCount { get; set; }
        public int LateDaysCount { get; set; }
        public int OnTimeDaysCount { get; set; }

        public WorkerAttendanceHistoryRowViewModel? LatestAttendance { get; set; }

        public List<WorkerAttendanceHistoryRowViewModel> History { get; set; } = new();
        public List<WorkerAttendanceHistoryRowViewModel> OTHistory { get; set; } = new();

        // Chart data fields
        public List<string> ChartLabels { get; set; } = new();
        public List<decimal> WorkedHoursChartData { get; set; } = new();
        public List<decimal> OTHoursChartData { get; set; } = new();
    }

    public class WorkerAttendanceHistoryRowViewModel
    {
        public DateTime Date { get; set; }
        public string InTime { get; set; } = "-";
        public string OutTime { get; set; } = "-";
        public string WorkedHours { get; set; } = "-";
        public string OTHours { get; set; } = "-";
        public decimal OTHoursValue { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
