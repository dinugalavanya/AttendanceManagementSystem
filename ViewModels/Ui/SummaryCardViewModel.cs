namespace AttendanceManagementSystem.ViewModels.Ui
{
    public class SummaryCardViewModel
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-circle";
        public string Tone { get; set; } = "neutral";
        public string? HelperText { get; set; }
    }
}
