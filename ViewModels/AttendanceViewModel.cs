using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.ViewModels
{
    public class AttendanceViewModel
    {
        public Attendance? TodayAttendance { get; set; }
        public bool CanCheckIn { get; set; }
        public bool CanCheckOut { get; set; }
        public DateTime CurrentTime { get; set; }
        
        // OT-related properties
        public string TodayOTDisplay { get; set; } = "0h 0m";
        public double TodayOTHours { get; set; }
        public string WeeklyOTDisplay { get; set; } = "0h 0m";
        public string MonthlyOTDisplay { get; set; } = "0h 0m";
        public bool HasOTToday { get; set; }
        
        // New properties for professional UI
        public string WorkedTimeDisplay { get; set; } = "0h 0m";
        public string CheckInTimeDisplay { get; set; } = "-";
        public string ScheduleEndTime { get; set; } = "04:30 PM";
        public string CurrentStatus { get; set; } = "Not Checked In";
        public string OvertimeHelperText { get; set; } = "No overtime yet";
        public int RegularWorkMinutes { get; set; }
        public int TotalWorkMinutes { get; set; }
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
