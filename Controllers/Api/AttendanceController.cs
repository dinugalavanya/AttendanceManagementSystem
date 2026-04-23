using Microsoft.AspNetCore.Mvc;
using AttendanceManagementSystem.Services;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceController : ControllerBase
    {
        private readonly IAttendanceService _attendanceService;
        private readonly ILogger<AttendanceController> _logger;

        public AttendanceController(IAttendanceService attendanceService, ILogger<AttendanceController> logger)
        {
            _attendanceService = attendanceService;
            _logger = logger;
        }

        /// <summary>
        /// Get attendance by ID
        /// </summary>
        /// <param name="id">Attendance ID</param>
        /// <returns>Attendance details</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAttendance(int id)
        {
            try
            {
                var attendance = await _attendanceService.GetAttendanceByIdAsync(id);
                
                if (attendance == null)
                {
                    return NotFound(new { success = false, message = "Attendance record not found" });
                }

                return Ok(new { 
                    success = true, 
                    attendance = new {
                        id = attendance.Id,
                        userId = attendance.UserId,
                        userName = $"{attendance.User?.FirstName} {attendance.User?.LastName}",
                        attendanceDate = attendance.AttendanceDate.ToString("yyyy-MM-dd"),
                        inTime = attendance.InTime?.ToString(@"hh\:mm\:ss"),
                        outTime = attendance.OutTime?.ToString(@"hh\:mm\:ss"),
                        status = attendance.Status,
                        totalWorkedMinutes = attendance.TotalWorkedMinutes,
                        overtimeMinutes = attendance.OvertimeMinutes,
                        regularWorkedMinutes = attendance.RegularWorkedMinutes,
                        isLocked = attendance.IsLocked,
                        notes = attendance.Notes
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attendance with ID: {Id}", id);
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get today's attendance for all users
        /// </summary>
        /// <returns>List of today's attendance records</returns>
        [HttpGet("today")]
        public async Task<IActionResult> GetTodayAttendance()
        {
            try
            {
                var today = DateTime.Today;
                var attendances = await _attendanceService.GetAllAttendancesAsync(today);

                var result = attendances.Select(a => new
                {
                    id = a.Id,
                    userId = a.UserId,
                    userName = $"{a.User?.FirstName} {a.User?.LastName}",
                    userEmail = a.User?.Email,
                    section = a.User?.Section?.Name,
                    attendanceDate = a.AttendanceDate.ToString("yyyy-MM-dd"),
                    inTime = a.InTime?.ToString(@"hh\:mm\:ss"),
                    outTime = a.OutTime?.ToString(@"hh\:mm\:ss"),
                    status = a.Status,
                    totalWorkedMinutes = a.TotalWorkedMinutes,
                    overtimeMinutes = a.OvertimeMinutes,
                    regularWorkedMinutes = a.RegularWorkedMinutes,
                    isLocked = a.IsLocked,
                    notes = a.Notes
                }).ToList();

                return Ok(new { 
                    success = true, 
                    date = today.ToString("yyyy-MM-dd"),
                    count = result.Count,
                    attendances = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today's attendance");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get attendance by user ID and date range
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="startDate">Start date (yyyy-MM-dd)</param>
        /// <param name="endDate">End date (yyyy-MM-dd)</param>
        /// <returns>User attendance records</returns>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserAttendance(int userId, [FromQuery] string? startDate = null, [FromQuery] string? endDate = null)
        {
            try
            {
                var start = string.IsNullOrEmpty(startDate) ? DateTime.Today.AddDays(-30) : DateTime.Parse(startDate);
                var end = string.IsNullOrEmpty(endDate) ? DateTime.Today : DateTime.Parse(endDate);

                var attendances = await _attendanceService.GetUserAttendancesAsync(userId, start, end);

                var result = attendances.Select(a => new
                {
                    id = a.Id,
                    attendanceDate = a.AttendanceDate.ToString("yyyy-MM-dd"),
                    inTime = a.InTime?.ToString(@"hh\:mm\:ss"),
                    outTime = a.OutTime?.ToString(@"hh\:mm\:ss"),
                    status = a.Status,
                    totalWorkedMinutes = a.TotalWorkedMinutes,
                    overtimeMinutes = a.OvertimeMinutes,
                    regularWorkedMinutes = a.RegularWorkedMinutes,
                    isLocked = a.IsLocked,
                    notes = a.Notes
                }).ToList();

                return Ok(new { 
                    success = true, 
                    userId = userId,
                    startDate = start.ToString("yyyy-MM-dd"),
                    endDate = end.ToString("yyyy-MM-dd"),
                    count = result.Count,
                    attendances = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attendance for user ID: {UserId}", userId);
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Check in user
        /// </summary>
        /// <param name="checkInRequest">Check in data</param>
        /// <returns>Check in result</returns>
        [HttpPost("checkin")]
        public async Task<IActionResult> CheckIn([FromBody] CheckInRequest checkInRequest)
        {
            try
            {
                var attendance = await _attendanceService.CheckInAsync(checkInRequest.UserId, checkInRequest.Notes);

                return Ok(new { 
                    success = true, 
                    message = "Check in successful",
                    attendance = new {
                        id = attendance.Id,
                        userId = attendance.UserId,
                        attendanceDate = attendance.AttendanceDate.ToString("yyyy-MM-dd"),
                        inTime = attendance.InTime?.ToString(@"hh\:mm\:ss"),
                        status = attendance.Status,
                        notes = attendance.Notes
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking in user ID: {UserId}", checkInRequest.UserId);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Check out user
        /// </summary>
        /// <param name="checkOutRequest">Check out data</param>
        /// <returns>Check out result</returns>
        [HttpPost("checkout")]
        public async Task<IActionResult> CheckOut([FromBody] CheckOutRequest checkOutRequest)
        {
            try
            {
                var attendance = await _attendanceService.CheckOutAsync(checkOutRequest.UserId, checkOutRequest.Notes);

                return Ok(new { 
                    success = true, 
                    message = "Check out successful",
                    attendance = new {
                        id = attendance.Id,
                        userId = attendance.UserId,
                        attendanceDate = attendance.AttendanceDate.ToString("yyyy-MM-dd"),
                        inTime = attendance.InTime?.ToString(@"hh\:mm\:ss"),
                        outTime = attendance.OutTime?.ToString(@"hh\:mm\:ss"),
                        status = attendance.Status,
                        totalWorkedMinutes = attendance.TotalWorkedMinutes,
                        overtimeMinutes = attendance.OvertimeMinutes,
                        regularWorkedMinutes = attendance.RegularWorkedMinutes,
                        notes = attendance.Notes
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking out user ID: {UserId}", checkOutRequest.UserId);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get attendance statistics
        /// </summary>
        /// <param name="sectionId">Section ID (optional)</param>
        /// <param name="startDate">Start date (yyyy-MM-dd)</param>
        /// <param name="endDate">End date (yyyy-MM-dd)</param>
        /// <returns>Attendance statistics</returns>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics([FromQuery] int? sectionId = null, [FromQuery] string? startDate = null, [FromQuery] string? endDate = null)
        {
            try
            {
                var start = string.IsNullOrEmpty(startDate) ? DateTime.Today.AddDays(-30) : DateTime.Parse(startDate);
                var end = string.IsNullOrEmpty(endDate) ? DateTime.Today : DateTime.Parse(endDate);

                var stats = await _attendanceService.GetAttendanceStatisticsAsync(sectionId, start, end);

                return Ok(new { 
                    success = true, 
                    sectionId = sectionId,
                    startDate = start.ToString("yyyy-MM-dd"),
                    endDate = end.ToString("yyyy-MM-dd"),
                    statistics = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attendance statistics");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }
    }

    public class CheckInRequest
    {
        public int UserId { get; set; }
        public string? Notes { get; set; }
    }

    public class CheckOutRequest
    {
        public int UserId { get; set; }
        public string? Notes { get; set; }
    }
}
