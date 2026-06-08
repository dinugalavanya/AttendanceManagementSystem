namespace AttendanceManagementSystem.Services
{
    public interface IEmailService
    {
        Task SendLoginCredentialsAsync(string toEmail, string fullName, string serviceId, string password, string role);
    }
}
