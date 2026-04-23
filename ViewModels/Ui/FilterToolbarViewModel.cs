namespace AttendanceManagementSystem.ViewModels.Ui
{
    public class FilterToolbarViewModel
    {
        public string FormAction { get; set; } = string.Empty;
        public string FormController { get; set; } = string.Empty;
        public DateTime SelectedDate { get; set; }
        public string DateFieldName { get; set; } = "date";
        public string DateLabel { get; set; } = "Date";
        public string? ScopeLabel { get; set; }
        public string? SummaryLabel { get; set; }
        public string? SummaryValue { get; set; }
        public string ApplyLabel { get; set; } = "Apply";
        public string TodayLabel { get; set; } = "Today";

        public DateTime PreviousDate => SelectedDate.AddDays(-1);
        public DateTime NextDate => SelectedDate.AddDays(1);
    }
}
