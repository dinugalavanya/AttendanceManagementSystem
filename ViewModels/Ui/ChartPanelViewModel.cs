namespace AttendanceManagementSystem.ViewModels.Ui
{
    public class ChartPanelViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string? Meta { get; set; }
        public string CanvasId { get; set; } = string.Empty;
        public string HeightClass { get; set; } = "chart-size-lg";
    }
}
