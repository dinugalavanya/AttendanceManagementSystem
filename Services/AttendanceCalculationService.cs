using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Services
{
    public class AttendanceCalculationService
    {
        // Constants for attendance calculation
        public static readonly TimeSpan OfficialStartTime = new TimeSpan(8, 30, 0); // 08:30
        public const int RequiredWorkMinutes = 480; // 8 hours * 60 minutes

        public AttendanceCalculationResult CalculateAttendance(DateTime attendanceDate, TimeSpan? inTime, TimeSpan? outTime, string? manualStatus = null)
        {
            var result = new AttendanceCalculationResult
            {
                AttendanceDate = attendanceDate,
                InTime = inTime,
                OutTime = outTime,
                ManualStatus = manualStatus
            };

            // If manual status is provided (Leave/Absent), override all calculations
            if (!string.IsNullOrEmpty(manualStatus))
            {
                if (manualStatus.Equals("Leave", StringComparison.OrdinalIgnoreCase) ||
                    manualStatus.Equals("Absent", StringComparison.OrdinalIgnoreCase))
                {
                    result.Status = manualStatus;
                    result.TotalWorkedMinutes = 0;
                    result.RegularWorkedMinutes = 0;
                    result.OvertimeMinutes = 0;
                    result.LateByMinutes = 0;
                    result.EarlyByMinutes = 0;
                    result.CurrentState = manualStatus.Equals("Leave", StringComparison.OrdinalIgnoreCase) ? "On Leave" : "Absent";
                    return result;
                }
            }

            // If no InTime, no work can be calculated
            if (!inTime.HasValue)
            {
                result.Status = "Absent";
                result.TotalWorkedMinutes = 0;
                result.RegularWorkedMinutes = 0;
                result.OvertimeMinutes = 0;
                result.LateByMinutes = 0;
                result.EarlyByMinutes = 0;
                result.CurrentState = "Absent";
                return result;
            }

            // Calculate status based on InTime
            if (inTime.Value <= OfficialStartTime)
            {
                result.Status = "On Time";
                result.LateByMinutes = 0;
                // Calculate early arrival if applicable
                if (inTime.Value < OfficialStartTime)
                {
                    result.EarlyByMinutes = (int)(OfficialStartTime - inTime.Value).TotalMinutes;
                }
                else
                {
                    result.EarlyByMinutes = 0;
                }
            }
            else
            {
                result.Status = "Late";
                result.LateByMinutes = (int)(inTime.Value - OfficialStartTime).TotalMinutes;
                result.EarlyByMinutes = 0;
            }

            // Calculate worked minutes if OutTime is available
            if (outTime.HasValue)
            {
                result.TotalWorkedMinutes = (int)(outTime.Value - inTime.Value).TotalMinutes;
                
                // Apply business rules for regular and overtime minutes
                if (result.TotalWorkedMinutes > RequiredWorkMinutes)
                {
                    result.RegularWorkedMinutes = RequiredWorkMinutes;
                    result.OvertimeMinutes = result.TotalWorkedMinutes - RequiredWorkMinutes;
                }
                else
                {
                    result.RegularWorkedMinutes = result.TotalWorkedMinutes;
                    result.OvertimeMinutes = 0;
                }

                // Determine current state
                result.CurrentState = DetermineCurrentState(inTime.Value, outTime.Value, result.OvertimeMinutes);
            }
            else
            {
                // Worker is still working or didn't check out
                result.TotalWorkedMinutes = 0;
                result.RegularWorkedMinutes = 0;
                result.OvertimeMinutes = 0;
                result.CurrentState = "Working";
            }

            return result;
        }

        private string DetermineCurrentState(TimeSpan inTime, TimeSpan? outTime, int overtimeMinutes)
        {
            if (!outTime.HasValue)
            {
                return "Working";
            }

            if (overtimeMinutes > 0)
            {
                return "Working OT";
            }

            return "Completed";
        }

        // Test method to validate calculation examples
        public void ValidateCalculationExamples()
        {
            var testDate = DateTime.Today;

            // Example 1: 08:10 → 16:30 → OT = 20 min (should be On Time)
            var result1 = CalculateAttendance(testDate, new TimeSpan(8, 10, 0), new TimeSpan(16, 30, 0));
            Console.WriteLine($"Example 1 - 08:10→16:30: Status={result1.Status}, Total={result1.TotalWorkedMinutes}, OT={result1.OvertimeMinutes} (Expected: On Time, 500, 20)");

            // Example 2: 08:10 → 16:10 → OT = 0 (should be On Time)
            var result2 = CalculateAttendance(testDate, new TimeSpan(8, 10, 0), new TimeSpan(16, 10, 0));
            Console.WriteLine($"Example 2 - 08:10→16:10: Status={result2.Status}, Total={result2.TotalWorkedMinutes}, OT={result2.OvertimeMinutes} (Expected: On Time, 480, 0)");

            // Example 3: 09:05 → 17:45 → OT = 40 min (should be Late)
            var result3 = CalculateAttendance(testDate, new TimeSpan(9, 5, 0), new TimeSpan(17, 45, 0));
            Console.WriteLine($"Example 3 - 09:05→17:45: Status={result3.Status}, Total={result3.TotalWorkedMinutes}, OT={result3.OvertimeMinutes} (Expected: Late, 520, 40)");

            // Example 4: 08:30 → 16:30 → OT = 0 (should be On Time)
            var result4 = CalculateAttendance(testDate, new TimeSpan(8, 30, 0), new TimeSpan(16, 30, 0));
            Console.WriteLine($"Example 4 - 08:30→16:30: Status={result4.Status}, Total={result4.TotalWorkedMinutes}, OT={result4.OvertimeMinutes} (Expected: On Time, 480, 0)");

            // Example 5: 08:31 → 16:31 → OT = 0 (should be Late)
            var result5 = CalculateAttendance(testDate, new TimeSpan(8, 31, 0), new TimeSpan(16, 31, 0));
            Console.WriteLine($"Example 5 - 08:31→16:31: Status={result5.Status}, Total={result5.TotalWorkedMinutes}, OT={result5.OvertimeMinutes} (Expected: Late, 480, 0)");

            // Example 6: 08:31 → 17:31 → OT = 60 min (should be Late)
            var result6 = CalculateAttendance(testDate, new TimeSpan(8, 31, 0), new TimeSpan(17, 31, 0));
            Console.WriteLine($"Example 6 - 08:31→17:31: Status={result6.Status}, Total={result6.TotalWorkedMinutes}, OT={result6.OvertimeMinutes} (Expected: Late, 540, 60)");
        }

        public string FormatMinutesToHours(int minutes)
        {
            if (minutes <= 0)
                return "-";

            var hours = minutes / 60;
            var mins = minutes % 60;
            return $"{hours}h {mins}m";
        }

        public string FormatTimeSpan(TimeSpan? time)
        {
            if (!time.HasValue)
                return "-";

            return time.Value.ToString(@"hh\:mm");
        }

        public string FormatLateByText(int lateByMinutes)
        {
            if (lateByMinutes <= 0)
                return "-";

            var hours = lateByMinutes / 60;
            var minutes = lateByMinutes % 60;
            
            if (hours > 0)
                return $"{hours}h {minutes}m";
            else
                return $"{minutes}m";
        }

        public string FormatEarlyByText(int earlyByMinutes)
        {
            if (earlyByMinutes <= 0)
                return "-";

            var hours = earlyByMinutes / 60;
            var minutes = earlyByMinutes % 60;
            
            if (hours > 0)
                return $"{hours}h {minutes}m";
            else
                return $"{minutes}m";
        }
    }

    public class AttendanceCalculationResult
    {
        public DateTime AttendanceDate { get; set; }
        public TimeSpan? InTime { get; set; }
        public TimeSpan? OutTime { get; set; }
        public string? ManualStatus { get; set; }
        
        public string Status { get; set; } = string.Empty;
        public int TotalWorkedMinutes { get; set; }
        public int RegularWorkedMinutes { get; set; }
        public int OvertimeMinutes { get; set; }
        public int LateByMinutes { get; set; }
        public int EarlyByMinutes { get; set; }
        public string CurrentState { get; set; } = string.Empty;

        // Computed properties for display
        public string WorkedHoursText => FormatMinutesToHours(TotalWorkedMinutes);
        public string RegularHoursText => FormatMinutesToHours(RegularWorkedMinutes);
        public string OvertimeHoursText => FormatMinutesToHours(OvertimeMinutes);
        public string LateByText => FormatLateByText(LateByMinutes);
        public string EarlyByText => FormatEarlyByText(EarlyByMinutes);
        public string InTimeText => FormatTimeSpan(InTime);
        public string OutTimeText => FormatTimeSpan(OutTime);

        private string FormatMinutesToHours(int minutes)
        {
            if (minutes <= 0)
                return "-";

            var hours = minutes / 60;
            var mins = minutes % 60;
            return $"{hours}h {mins}m";
        }

        private string FormatTimeSpan(TimeSpan? time)
        {
            if (!time.HasValue)
                return "-";

            return time.Value.ToString(@"hh\:mm");
        }

        private string FormatLateByText(int lateByMinutes)
        {
            if (lateByMinutes <= 0)
                return "-";

            var hours = lateByMinutes / 60;
            var minutes = lateByMinutes % 60;
            
            if (hours > 0)
                return $"{hours}h {minutes}m";
            else
                return $"{minutes}m";
        }

        private string FormatEarlyByText(int earlyByMinutes)
        {
            if (earlyByMinutes <= 0)
                return "-";

            var hours = earlyByMinutes / 60;
            var minutes = earlyByMinutes % 60;
            
            if (hours > 0)
                return $"{hours}h {minutes}m";
            else
                return $"{minutes}m";
        }
    }
}
