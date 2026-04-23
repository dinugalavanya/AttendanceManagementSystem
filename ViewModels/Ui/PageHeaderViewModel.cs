namespace AttendanceManagementSystem.ViewModels.Ui
{
    public class PageHeaderViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string? Eyebrow { get; set; }
        public List<PageHeaderBadgeViewModel> Badges { get; set; } = new();
    }

    public class PageHeaderBadgeViewModel
    {
        public string Text { get; set; } = string.Empty;
        public string Tone { get; set; } = "neutral";
        public string Icon { get; set; } = "bi-circle";
    }
}
