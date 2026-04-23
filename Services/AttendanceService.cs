using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly ApplicationDbContext _context;

        public AttendanceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Attendance?> GetTodayAttendanceAsync(int userId)
        {
            var today = DateTime.Today;
            return await _context.Attendances
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.UserId == userId && a.AttendanceDate.Date == today);
        }

        public async Task<Attendance?> GetAttendanceByIdAsync(int attendanceId)
        {
            return await _context.Attendances
                .AsNoTracking()
                .Include(a => a.User)
                .Include(a => a.EditLogs)
                .ThenInclude(el => el.EditedByUser)
                .FirstOrDefaultAsync(a => a.Id == attendanceId);
        }

        public async Task<Attendance> CheckInAsync(int userId, string? notes = null)
        {
            var today = DateTime.Today;
            var existingAttendance = await GetTodayAttendanceAsync(userId);

            if (existingAttendance != null)
            {
                throw new InvalidOperationException("You have already checked in today.");
            }

            var now = DateTime.Now;
            var attendance = new Attendance
            {
                UserId = userId,
                AttendanceDate = today,
                InTime = now.TimeOfDay,
                Status = DetermineCheckInStatus(now.TimeOfDay),
                IsLocked = false,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };

            await CalculateWorkHours(attendance);

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();

            return attendance;
        }

        public async Task<Attendance> CheckOutAsync(int userId, string? notes = null)
        {
            var today = DateTime.Today;
            var attendance = await GetTodayAttendanceAsync(userId);

            if (attendance == null)
            {
                throw new InvalidOperationException("You haven't checked in today.");
            }

            if (attendance.IsLocked)
            {
                throw new InvalidOperationException("Your attendance has already been locked for today.");
            }

            var now = DateTime.Now;
            attendance.OutTime = now.TimeOfDay;
            attendance.IsLocked = true;
            attendance.Notes = notes;
            attendance.UpdatedAt = DateTime.UtcNow;

            await CalculateWorkHours(attendance);

            await _context.SaveChangesAsync();

            return attendance;
        }

        public async Task<Attendance> UpdateAttendanceAsync(int attendanceId, TimeSpan? inTime, TimeSpan? outTime, string status, int editedByUserId, string? editReason = null)
        {
            var attendance = await _context.Attendances
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == attendanceId);

            if (attendance == null)
            {
                throw new InvalidOperationException("Attendance record not found.");
            }

            // Store old values for audit log
            var oldInTime = attendance.InTime;
            var oldOutTime = attendance.OutTime;
            var oldStatus = attendance.Status;
            var oldTotalMinutes = attendance.TotalWorkedMinutes;
            var oldRegularMinutes = attendance.RegularWorkedMinutes;
            var oldOvertimeMinutes = attendance.OvertimeMinutes;

            // Update attendance
            attendance.InTime = inTime;
            attendance.OutTime = outTime;
            attendance.Status = status;
            attendance.UpdatedAt = DateTime.UtcNow;

            await CalculateWorkHours(attendance);

            // Create edit log
            if (oldInTime != inTime || oldOutTime != outTime || oldStatus != status)
            {
                var editLog = new AttendanceEditLog
                {
                    AttendanceId = attendanceId,
                    EditedByUserId = editedByUserId,
                    EditReason = editReason,
                    OldInTime = oldInTime,
                    OldOutTime = oldOutTime,
                    OldStatus = oldStatus,
                    OldTotalWorkedMinutes = oldTotalMinutes,
                    OldRegularWorkedMinutes = oldRegularMinutes,
                    OldOvertimeMinutes = oldOvertimeMinutes,
                    NewInTime = inTime,
                    NewOutTime = outTime,
                    NewStatus = status,
                    NewTotalWorkedMinutes = attendance.TotalWorkedMinutes,
                    NewRegularWorkedMinutes = attendance.RegularWorkedMinutes,
                    NewOvertimeMinutes = attendance.OvertimeMinutes,
                    EditedAt = DateTime.UtcNow
                };

                _context.AttendanceEditLogs.Add(editLog);
            }

            await _context.SaveChangesAsync();

            return attendance;
        }

        public async Task<List<Attendance>> GetUserAttendancesAsync(int userId, DateTime startDate, DateTime endDate)
        {
            return await _context.Attendances
                .AsNoTracking()
                .Include(a => a.User)
                .Include(a => a.EditLogs)
                .Where(a => a.UserId == userId && a.AttendanceDate.Date >= startDate.Date && a.AttendanceDate.Date <= endDate.Date)
                .OrderByDescending(a => a.AttendanceDate)
                .ToListAsync();
        }

        public async Task<List<Attendance>> GetSectionAttendancesAsync(int sectionId, DateTime date)
        {
            return await _context.Attendances
                .AsNoTracking()
                .Include(a => a.User)
                .ThenInclude(u => u.Section)
                .Include(a => a.EditLogs)
                .Where(a => a.User.SectionId == sectionId && a.AttendanceDate.Date == date.Date)
                .OrderBy(a => a.User.FirstName)
                .ThenBy(a => a.User.LastName)
                .ToListAsync();
        }

        public async Task<List<Attendance>> GetAllAttendancesAsync(DateTime date)
        {
            return await _context.Attendances
                .AsNoTracking()
                .Include(a => a.User)
                .ThenInclude(u => u.Section)
                .Include(a => a.EditLogs)
                .Where(a => a.AttendanceDate.Date == date.Date)
                .OrderBy(a => a.User.Section != null ? a.User.Section.Name : "")
                .ThenBy(a => a.User.FirstName)
                .ThenBy(a => a.User.LastName)
                .ToListAsync();
        }

        public Task CalculateWorkHours(Attendance attendance)
        {
            if (!attendance.InTime.HasValue)
            {
                attendance.TotalWorkedMinutes = 0;
                attendance.RegularWorkedMinutes = 0;
                attendance.OvertimeMinutes = 0;
                return Task.CompletedTask;
            }

            var outTime = attendance.OutTime ?? DateTime.UtcNow.TimeOfDay;
            var totalMinutes = (int)(outTime - attendance.InTime.Value).TotalMinutes;

            if (totalMinutes < 0) totalMinutes = 0;

            attendance.TotalWorkedMinutes = totalMinutes;

            // Calculate regular and overtime hours
            var workStart = WorkingHours.WorkStart;
            var workEnd = WorkingHours.WorkEnd;

            var actualStart = attendance.InTime.Value < workStart ? workStart : attendance.InTime.Value;
            var actualEnd = outTime > workEnd ? workEnd : outTime;

            if (actualEnd > actualStart)
            {
                attendance.RegularWorkedMinutes = (int)(actualEnd - actualStart).TotalMinutes;
            }
            else
            {
                attendance.RegularWorkedMinutes = 0;
            }

            // Calculate overtime (time after 4:30 PM)
            if (outTime > workEnd)
            {
                attendance.OvertimeMinutes = (int)(outTime - workEnd).TotalMinutes;
            }
            else
            {
                attendance.OvertimeMinutes = 0;
            }

            // Don't count overtime if total worked is less than regular hours
            if (attendance.TotalWorkedMinutes < attendance.RegularWorkedMinutes)
            {
                attendance.RegularWorkedMinutes = attendance.TotalWorkedMinutes;
                attendance.OvertimeMinutes = 0;
            }

            return Task.CompletedTask;
        }

        public async Task<Dictionary<string, int>> GetAttendanceStatisticsAsync(int? sectionId, DateTime startDate, DateTime endDate)
        {
            var query = _context.Attendances
                .AsNoTracking()
                .Include(a => a.User)
                .Where(a => a.AttendanceDate.Date >= startDate.Date && a.AttendanceDate.Date <= endDate.Date);

            if (sectionId.HasValue)
            {
                query = query.Where(a => a.User.SectionId == sectionId);
            }

            var attendances = await query.ToListAsync();

            return new Dictionary<string, int>
            {
                ["Total"] = attendances.Count,
                ["Present"] = attendances.Count(a => a.Status == AttendanceStatus.Present),
                ["Absent"] = attendances.Count(a => a.Status == AttendanceStatus.Absent),
                ["Late"] = attendances.Count(a => a.Status == AttendanceStatus.Late),
                ["HalfDay"] = attendances.Count(a => a.Status == AttendanceStatus.HalfDay),
                ["Leave"] = attendances.Count(a => a.Status == AttendanceStatus.Leave)
            };
        }

        private string DetermineCheckInStatus(TimeSpan inTime)
        {
            var workStart = WorkingHours.WorkStart;
            
            if (inTime > workStart + TimeSpan.FromMinutes(15))
            {
                return AttendanceStatus.Late;
            }
            
            return AttendanceStatus.Present;
        }
    }
}
