using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttendanceManagementSystem.Models
{
    public class Role
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }

    public static class RoleNames
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string Admin = "Admin";
        public const string GM = "GM";
        public const string DGM = "DGM";
        public const string Engineer = "Engineer";
        public const string RoleAgent = "Role agent";
        public const string LeaveAgent = "Leave agent";
        public const string Worker = "Worker";

        public const string EngineerAccessRoles = "SuperAdmin,Admin,GM,DGM,Engineer,Role agent,Leave agent";

        public static bool HasEngineerPrivileges(string? roleName)
        {
            return roleName == Engineer || roleName == RoleAgent || roleName == LeaveAgent;
        }
    }
}
