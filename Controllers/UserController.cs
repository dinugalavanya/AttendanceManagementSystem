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
        private readonly IEmailService _emailService;

        public UserController(IAuthService authService, ApplicationDbContext context, IEmailService emailService)
        {
            _authService = authService;
            _context = context;
            _emailService = emailService;
        }

        // GET: User
        public async Task<IActionResult> Index()
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.IsSuperAdmin = currentUser.Role?.Name == RoleNames.SuperAdmin;

            // Load active sections for the Create Admin modal
            ViewBag.Sections = await _context.Sections
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var isSuperAdmin = currentUser.Role?.Name == RoleNames.SuperAdmin;

            var usersQuery = _context.Users
                .Include(u => u.Role)
                .Include(u => u.Section)
                .AsQueryable();

            if (isSuperAdmin)
                usersQuery = usersQuery.Where(u => u.Role.Name == RoleNames.Admin);

            var users = await usersQuery
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new UserListItemViewModel
                {
                    Id = u.Id,
                    ServiceId = u.ServiceId,
                    FullName = u.FirstName + " " + u.LastName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Address = u.Address,
                    RoleName = u.Role.Name,
                    SectionName = u.Section != null ? u.Section.Name : "-"
                })
                .ToListAsync();

            return View(users);
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

                // Auto-generate a secure temporary password
                string tempPassword = GenerateSecurePassword();
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(tempPassword);

                // Create new admin user
                var newAdmin = new User
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = request.Email.Trim().ToLower(),
                    PasswordHash = hashedPassword,
                    Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                    Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
                    ServiceId = await GenerateNextEmployeeServiceIdAsync(),
                    RoleId = 2, // Admin role
                    IsActive = true,
                    SectionId = request.SectionId.Value, // Assign selected section
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newAdmin);
                await _context.SaveChangesAsync();

                // Send login credentials to admin's email
                try
                {
                    await _emailService.SendLoginCredentialsAsync(newAdmin.Email, newAdmin.FullName, newAdmin.ServiceId, tempPassword, "Admin");
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"[EMAIL ERROR] Failed to send email to {newAdmin.Email}: {emailEx.Message}");
                }

                return Json(new { 
                    success = true, 
                    message = "Admin created successfully. Login credentials have been sent to their email."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error creating admin: {ex.Message}" });
            }
        }

        // POST: User/CreateEmployee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEmployee(CreateEmployeeViewModel model)
        {
            try
            {
                var currentUser = _authService.GetCurrentUser();
                if (currentUser == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Only Admin can add employees
                if (currentUser.Role?.Name != RoleNames.Admin && currentUser.Role?.Name != RoleNames.SuperAdmin)
                {
                    return RedirectToAction("Index", "Dashboard");
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(model.FirstName) ||
                    string.IsNullOrWhiteSpace(model.LastName) ||
                    string.IsNullOrWhiteSpace(model.Email) ||
                    string.IsNullOrWhiteSpace(model.Password))
                {
                    ModelState.AddModelError("", "All required fields must be filled.");
                    return View(model);
                }

                // Validate password confirmation
                if (model.Password != model.ConfirmPassword)
                {
                    ModelState.AddModelError("", "Password and confirmation password do not match.");
                    return View(model);
                }

                // Get current admin's section for validation
                var currentAdmin = await _context.Users
                    .Include(u => u.Section)
                    .FirstOrDefaultAsync(u => u.Id == currentUser.Id);

                // Validate section assignment
                int? adminSectionId = currentAdmin?.SectionId;
                int? requestSectionId = model.SectionId;

                // If admin tries to assign a different section, ignore and use admin's section
                if (requestSectionId.HasValue && adminSectionId.HasValue && requestSectionId != adminSectionId)
                {
                    requestSectionId = adminSectionId;
                }

                // Check if email already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());
                
                if (existingUser != null)
                {
                    ModelState.AddModelError("", "Email already exists.");
                    return View(model);
                }

                // Get Worker role (RoleId = 3)
                var workerRole = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Id == 3);
                
                if (workerRole == null)
                {
                    ModelState.AddModelError("", "Worker role not found.");
                    return View(model);
                }

                // Hash password using BCrypt
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);

                var newServiceId = await GenerateNextEmployeeServiceIdAsync();

                // Create new employee with Worker role
                var newEmployee = new User
                {
                    FirstName = model.FirstName.Trim(),
                    LastName = model.LastName.Trim(),
                    Email = model.Email.Trim().ToLower(),
                    PasswordHash = hashedPassword,
                    Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
                    Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim(),
                    RoleId = 3, // Worker role
                    IsActive = true,
                    SectionId = requestSectionId, // Use validated section
                    ServiceId = newServiceId,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newEmployee);
                await _context.SaveChangesAsync();

                // Add success message to TempData
                TempData["SuccessMessage"] = "Employee created successfully.";

                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating employee: {ex.Message}");
                return View(model);
            }
        }

        // GET: User/LookupAdmin?serviceId=EMP901
        [HttpGet]
        public async Task<IActionResult> LookupAdmin(string serviceId)
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser?.Role?.Name != RoleNames.SuperAdmin)
                return Json(new { success = false, message = "Unauthorized." });

            var admin = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Section)
                .FirstOrDefaultAsync(u => u.ServiceId == serviceId && u.Role.Name == RoleNames.Admin);

            if (admin == null)
                return Json(new { success = false, message = "No Admin found with that Service ID." });

            return Json(new
            {
                success = true,
                fullName = admin.FirstName + " " + admin.LastName,
                serviceId = admin.ServiceId,
                sectionName = admin.Section?.Name ?? "Not Assigned"
            });
        }

        // POST: User/ChangeAdminSection
        [HttpPost]
        public async Task<IActionResult> ChangeAdminSection([FromBody] ChangeAdminSectionRequest request)
        {
            var currentUser = _authService.GetCurrentUser();
            if (currentUser?.Role?.Name != RoleNames.SuperAdmin)
                return Json(new { success = false, message = "Unauthorized." });

            var admin = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.ServiceId == request.ServiceId && u.Role.Name == RoleNames.Admin);

            if (admin == null)
                return Json(new { success = false, message = "Admin not found." });

            var section = await _context.Sections.FirstOrDefaultAsync(s => s.Id == request.SectionId && s.IsActive);
            if (section == null)
                return Json(new { success = false, message = "Invalid section." });

            admin.SectionId = request.SectionId;
            admin.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Section changed to '{section.Name}' successfully." });
        }

        private async Task<string> GenerateNextEmployeeServiceIdAsync()
        {
            var existingServiceIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.ServiceId != null)
                .Select(u => u.ServiceId!)
                .ToListAsync();

            var maxNumber = existingServiceIds
                .Select(ParseEmployeeServiceNumber)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .DefaultIfEmpty(0)
                .Max();

            return $"EMP{maxNumber + 1:D3}";
        }

        private static string GenerateSecurePassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnpqrstuvwxyz";
            const string digits = "23456789";
            const string special = "!@#$%^&*";
            const string all = upper + lower + digits + special;
            var rng = new Random();
            var chars = new char[12];
            chars[0] = upper[rng.Next(upper.Length)];
            chars[1] = lower[rng.Next(lower.Length)];
            chars[2] = digits[rng.Next(digits.Length)];
            chars[3] = special[rng.Next(special.Length)];
            for (int i = 4; i < 12; i++) chars[i] = all[rng.Next(all.Length)];
            return new string(chars.OrderBy(_ => rng.Next()).ToArray());
        }

        private static int? ParseEmployeeServiceNumber(string serviceId)
        {
            var normalized = serviceId.Trim().ToUpperInvariant();
            if (!normalized.StartsWith("EMP", StringComparison.Ordinal))
            {
                return null;
            }

            var digitsOnly = new string(normalized
                .Skip(3)
                .TakeWhile(char.IsDigit)
                .ToArray());

            if (string.IsNullOrWhiteSpace(digitsOnly))
            {
                return null;
            }

            return int.TryParse(digitsOnly, out var parsed) ? parsed : null;
        }

    }

    public class UserListItemViewModel
    {
        public int Id { get; set; }
        public string ServiceId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
    }

    public class CreateEmployeeViewModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public int? SectionId { get; set; }
    }

    public class CreateAdminRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int? SectionId { get; set; }
    }

    public class ChangeAdminSectionRequest
    {
        public string ServiceId { get; set; } = string.Empty;
        public int SectionId { get; set; }
    }
}
