using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceManagementSystem.Models
{
    public class Attendance
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public DateTime AttendanceDate { get; set; }

        public TimeSpan? InTime { get; set; }

        public TimeSpan? OutTime { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = AttendanceStatus.Present;

        public int TotalWorkedMinutes { get; set; }

        public int OvertimeMinutes { get; set; }

        public int RegularWorkedMinutes { get; set; }

        public bool IsLocked { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        public virtual ICollection<AttendanceEditLog> EditLogs { get; set; } = new List<AttendanceEditLog>();

        [NotMapped]
        public string TotalWorkedDisplay => FormatMinutes(TotalWorkedMinutes);

        [NotMapped]
        public string RegularWorkedDisplay => FormatMinutes(RegularWorkedMinutes);

        [NotMapped]
        public string OvertimeDisplay => FormatMinutes(OvertimeMinutes);

        private static string FormatMinutes(int minutes)
        {
            if (minutes <= 0)
            {
                return "0h 00m";
            }

            var hours = minutes / 60;
            var remainingMinutes = minutes % 60;
            return $"{hours}h {remainingMinutes:00}m";
        }
    }

    public static class AttendanceStatus
    {
        public const string Present = "Present";
        public const string Late = "Late";
        public const string Absent = "Absent";
        public const string HalfDay = "HalfDay";
        public const string Leave = "Leave";
    }

    public static class WorkingHours
    {
        public static readonly TimeSpan WorkStart = new(8, 30, 0);
        public static readonly TimeSpan WorkEnd = new(16, 30, 0);
    }
}
