using System.ComponentModel.DataAnnotations;

namespace AttendanceManagementSystem.DTOs
{
    public class AttendanceUpdateDTO
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string InTime { get; set; } = string.Empty; // "HH:mm" format

        [Required]
        [StringLength(100)]
        public string OutTime { get; set; } = string.Empty; // "HH:mm" format

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = string.Empty;
    }
}
