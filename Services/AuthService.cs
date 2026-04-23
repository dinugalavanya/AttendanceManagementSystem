using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<User?> AuthenticateAsync(string email, string password)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Section)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive);

            if (user == null) return null;

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<User?> LoginAsync(string email, string password)
        {
            return await AuthenticateAsync(email, password);
        }

        public async Task<bool> RegisterAsync(User user)
        {
            try
            {
                // Check if email already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == user.Email.ToLower());

                if (existingUser != null) return false;

                // Add new user
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Section)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        }

        public async Task<bool> IsUserInRoleAsync(int userId, string roleName)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            return user?.Role?.Name == roleName;
        }

        public async Task<bool> HasSectionAccessAsync(int userId, int sectionId)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Section)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (user == null) return false;

            // Super Admin has access to all sections
            if (user.Role.Name == RoleNames.SuperAdmin) return true;

            // Admin has access to their own section
            if (user.Role.Name == RoleNames.Admin && user.SectionId == sectionId) return true;

            return false;
        }

        public void SignIn(User user, bool rememberMe = false)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role.Name),
                new Claim("SectionId", user.SectionId?.ToString() ?? ""),
                new Claim("FirstName", user.FirstName),
                new Claim("LastName", user.LastName)
            };

            var identity = new ClaimsIdentity(claims, "CookieAuth");
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            _httpContextAccessor.HttpContext?.SignInAsync("CookieAuth", principal, authProperties);
        }

        public void SignOut()
        {
            _httpContextAccessor.HttpContext?.SignOutAsync("CookieAuth");
        }

        public int? GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return null;

            // Always fetch from database to ensure fresh data and avoid serialization cycles
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Section)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            return user;
        }

        public User? GetCurrentUser()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return null;

            try
            {
                // Synchronous version for backward compatibility
                var user = _context.Users
                    .Include(u => u.Role)
                    .Include(u => u.Section)
                    .FirstOrDefault(u => u.Id == userId && u.IsActive);

                return user;
            }
            catch (Exception)
            {
                // If there's any issue, return null rather than throwing
                return null;
            }
        }
    }
}
