using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceManagementSystem.Models
{
    public class AttendanceEditLog
    {
        public int Id { get; set; }

        [Required]
        public int AttendanceId { get; set; }

        [Required]
        public int EditedByUserId { get; set; }

        [StringLength(1000)]
        public string? EditReason { get; set; }

        public TimeSpan? OldInTime { get; set; }
        public TimeSpan? OldOutTime { get; set; }

        [StringLength(20)]
        public string? OldStatus { get; set; }

        public int? OldTotalWorkedMinutes { get; set; }
        public int? OldOvertimeMinutes { get; set; }
        public int? OldRegularWorkedMinutes { get; set; }

        public TimeSpan? NewInTime { get; set; }
        public TimeSpan? NewOutTime { get; set; }

        [StringLength(20)]
        public string? NewStatus { get; set; }

        public int? NewTotalWorkedMinutes { get; set; }
        public int? NewOvertimeMinutes { get; set; }
        public int? NewRegularWorkedMinutes { get; set; }

        public DateTime EditedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("AttendanceId")]
        public virtual Attendance Attendance { get; set; } = null!;

        [ForeignKey("EditedByUserId")]
        public virtual User EditedByUser { get; set; } = null!;
    }
}
