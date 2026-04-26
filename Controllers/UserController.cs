using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AttendanceManagementSystem.Services;
using AttendanceManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _context;

        public UserController(IAuthService authService, ApplicationDbContext context)
        {
            _authService = authService;
            _context = context;
        }

        // GET: User
        public async Task<IActionResult> Index()
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Check if current user is SuperAdmin
            ViewBag.IsSuperAdmin = currentUser.Role?.Name == RoleNames.SuperAdmin;

            // Load active sections for the Create Admin modal
            ViewBag.Sections = await _context.Sections
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var users = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Section)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new UserListItemViewModel
                {
                    Id = u.Id,
                    FullName = u.FirstName + " " + u.LastName,
                    Email = u.Email,
                    RoleName = u.Role.Name,
                    SectionName = u.Section != null ? u.Section.Name : "-",
                    IsActive = u.IsActive,
                    LoginTime = u.LoginTime,
                    LogoutTime = u.LogoutTime,
                    AttendanceStatus = u.AttendanceStatus
                })
                .ToListAsync();

            return View(users);
        }

        // POST: api/user/update-attendance
        [HttpPost]
        public async Task<IActionResult> UpdateAttendance([FromBody] AttendanceUpdateRequest request)
        {
            try
            {
                var currentUser = _authService.GetCurrentUser();
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Update the appropriate field
                switch (request.Field.ToLower())
                {
                    case "login":
                        if (string.IsNullOrEmpty(request.Value))
                        {
                            user.LoginTime = null;
                        }
                        else if (TimeSpan.TryParse(request.Value, out TimeSpan loginTime))
                        {
                            user.LoginTime = loginTime;
                        }
                        else
                        {
                            return Json(new { success = false, message = "Invalid login time format" });
                        }
                        break;

                    case "logout":
                        if (string.IsNullOrEmpty(request.Value))
                        {
                            user.LogoutTime = null;
                        }
                        else if (TimeSpan.TryParse(request.Value, out TimeSpan logoutTime))
                        {
                            user.LogoutTime = logoutTime;
                        }
                        else
                        {
                            return Json(new { success = false, message = "Invalid logout time format" });
                        }
                        break;

                    case "status":
                        var validStatuses = new[] { "On Time", "Late", "Leave" };
                        if (string.IsNullOrEmpty(request.Value))
                        {
                            user.AttendanceStatus = null;
                        }
                        else if (validStatuses.Contains(request.Value))
                        {
                            user.AttendanceStatus = request.Value;
                        }
                        else
                        {
                            return Json(new { success = false, message = "Invalid status value" });
                        }
                        break;

                    default:
                        return Json(new { success = false, message = "Invalid request" });
                }

                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Attendance updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating attendance: " + ex.Message });
            }
        }

        // POST: User/CreateAdmin
        [HttpPost]
        public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminRequest request)
        {
            try
            {
                var currentUser = _authService.GetCurrentUser();
                if (currentUser == null || currentUser.Role?.Name != RoleNames.SuperAdmin)
                {
                    return Json(new { success = false, message = "Unauthorized. Only SuperAdmin can create Admin users." });
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.FirstName) ||
                    string.IsNullOrWhiteSpace(request.LastName) ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password) ||
                    !request.SectionId.HasValue)
                {
                    return Json(new { success = false, message = "All required fields must be filled." });
                }

                // Check if email already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());
                
                if (existingUser != null)
                {
                    return Json(new { success = false, message = "Email already exists." });
                }

                // Validate SectionId exists and is active
                var selectedSection = await _context.Sections
                    .FirstOrDefaultAsync(s => s.Id == request.SectionId.Value && s.IsActive);
                
                if (selectedSection == null)
                {
                    return Json(new { success = false, message = "Invalid section selected." });
                }

                // Get Admin role (RoleId = 2)
                var adminRole = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Id == 2);
                
                if (adminRole == null)
                {
                    return Json(new { success = false, message = "Admin role not found." });
                }

                // Hash password using BCrypt
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

                // Create new admin user
                var newAdmin = new User
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = request.Email.Trim().ToLower(),
                    PasswordHash = hashedPassword,
                    Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                    Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
                    RoleId = 2, // Admin role
                    IsActive = true,
                    SectionId = request.SectionId.Value, // Assign selected section
                    UpdatedAt = DateTime.UtcNow,
                    LoginTime = null,
                    LogoutTime = null,
                    AttendanceStatus = null
                };

                _context.Users.Add(newAdmin);
                await _context.SaveChangesAsync();

                return Json(new { 
                    success = true, 
                    message = "Admin created successfully." 
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error creating admin: {ex.Message}" });
            }
        }
    }

    public class UserListItemViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        
        // Attendance fields
        public TimeSpan? LoginTime { get; set; }
        public TimeSpan? LogoutTime { get; set; }
        public string? AttendanceStatus { get; set; }
    }

    public class AttendanceUpdateRequest
    {
        public int UserId { get; set; }
        public string Field { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class CreateAdminRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int? SectionId { get; set; }
    }
}
