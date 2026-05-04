using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceManagementSystem.ViewModels
{
    public class AttendanceUpdateViewModel
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [Display(Name = "Employee Name")]
        public string EmployeeName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "In Time")]
        public TimeSpan? InTime { get; set; }

        [Required]
        [Display(Name = "Out Time")]
        public TimeSpan? OutTime { get; set; }

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Present";

        [Required]
        [Display(Name = "Attendance Date")]
        public DateTime AttendanceDate { get; set; }

        // Calculated property (not editable)
        [Display(Name = "Worked Hours")]
        public decimal WorkedHours { get; set; }

        // Calculated property (not editable)
        [Display(Name = "OT Hours")]
        public decimal OTHours { get; set; }

        // Helper properties for HTML time input compatibility
        [NotMapped]
        public string? InTimeString { get; set; }

        [NotMapped]
        public string? OutTimeString { get; set; }
    }
}
