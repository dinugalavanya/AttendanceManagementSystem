namespace AttendanceManagementSystem.ViewModels
{
    public class WorkerOtSearchRequestViewModel
    {
        public string? ServiceId { get; set; }
        public DateTime? SelectedDate { get; set; }
        public DateTime? SingleDate { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public string? NormalizedServiceId => string.IsNullOrWhiteSpace(ServiceId)
            ? null
            : ServiceId.Trim().ToUpperInvariant();
    }
}
