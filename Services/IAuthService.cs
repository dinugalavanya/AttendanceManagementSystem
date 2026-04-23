using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Services
{
    public interface IAuthService
    {
        Task<User?> AuthenticateAsync(string email, string password);
        Task<User?> LoginAsync(string email, string password);
        Task<bool> RegisterAsync(User user);
        Task<User?> GetUserByIdAsync(int userId);
        Task<bool> IsUserInRoleAsync(int userId, string roleName);
        Task<bool> HasSectionAccessAsync(int userId, int sectionId);
        void SignIn(User user, bool rememberMe = false);
        void SignOut();
        int? GetCurrentUserId();
        User? GetCurrentUser();
        Task<User?> GetCurrentUserAsync();
    }
}
