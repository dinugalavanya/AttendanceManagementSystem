using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceManagementSystem.Models
{
    public class Section
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }

    public static class SectionIds
    {
        public const int AllSections = -1;
        public const int AllSectionsLegacy = 0;

        /// <summary>
        /// Reserved section IDs (0, -1) are only valid for SuperAdmin and GM — not workers or section admins.
        /// </summary>
        public static bool IsReserved(int? sectionId) =>
            sectionId.HasValue && sectionId.Value <= 0;

        /// <summary>
        /// A real department section that workers and section admins can be assigned to.
        /// </summary>
        public static bool IsAssignable(int? sectionId) =>
            sectionId.HasValue && sectionId.Value > 0;
    }
}
