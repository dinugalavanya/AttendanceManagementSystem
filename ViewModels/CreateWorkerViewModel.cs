using System.ComponentModel.DataAnnotations;

namespace AttendanceManagementSystem.ViewModels
{
    public class CreateWorkerViewModel
    {
        [Required]
        [StringLength(150)]
        public string WorkerName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string ServiceId { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;
    }
}
