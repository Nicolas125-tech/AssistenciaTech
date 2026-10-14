using AssistenciaTech.Services;

namespace AssistenciaTech.ViewModels
{
    public class AdminDashboardViewModel
    {
        public DashboardDto DashboardData { get; set; } = new DashboardDto();
        public string? SearchString { get; set; }
        public string? StatusFilter { get; set; }
        public string? ErroBanco { get; set; }
    }
}
