using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Services
{
    public interface IAttendanceService
    {
        Task<Attendance?> GetTodayAttendanceAsync(int userId);
        Task<Attendance?> GetAttendanceByIdAsync(int attendanceId);
        Task<Attendance> CheckInAsync(int userId, string? notes = null);
        Task<Attendance> CheckOutAsync(int userId, string? notes = null);
        Task<Attendance> UpdateAttendanceAsync(int attendanceId, TimeSpan? inTime, TimeSpan? outTime, string status, int editedByUserId, string? editReason = null);
        Task<List<Attendance>> GetUserAttendancesAsync(int userId, DateTime startDate, DateTime endDate);
        Task<List<Attendance>> GetSectionAttendancesAsync(int sectionId, DateTime date);
        Task<List<Attendance>> GetAllAttendancesAsync(DateTime date);
        Task CalculateWorkHours(Attendance attendance);
        Task<Dictionary<string, int>> GetAttendanceStatisticsAsync(int? sectionId, DateTime startDate, DateTime endDate);
    }
}
