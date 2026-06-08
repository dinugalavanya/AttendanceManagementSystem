using System.ComponentModel.DataAnnotations;

namespace AttendanceManagementSystem.ViewModels
{
    public class DeleteUserViewModel
    {
        [Required]
        [StringLength(50)]
        public string ServiceId { get; set; } = string.Empty;
    }
}