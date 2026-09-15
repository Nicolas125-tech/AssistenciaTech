using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AssistenciaTech.Services
{
    public class AdminFacade : IAdminFacade
    {
        public IEstoqueService Estoque { get; }
        public IWebHostEnvironment Env { get; }
        public IPdfGeneratorService PdfGenerator { get; }
        public IAdminDashboardService Dashboard { get; }
        public IEquipamentoBackupService EquipamentoBackup { get; }
        public IServiceScopeFactory ScopeFactory { get; }
        public INotificationService Notification { get; }
        public IClienteService Cliente { get; }

        public AdminFacade(
            IEstoqueService estoqueService,
            IWebHostEnvironment env,
            IPdfGeneratorService pdfGeneratorService,
            IAdminDashboardService dashboardService,
            IEquipamentoBackupService equipamentoBackupService,
            IServiceScopeFactory scopeFactory,
            INotificationService notificationService,
            IClienteService clienteService)
        {
            Estoque = estoqueService;
            Env = env;
            PdfGenerator = pdfGeneratorService;
            Dashboard = dashboardService;
            EquipamentoBackup = equipamentoBackupService;
            ScopeFactory = scopeFactory;
            Notification = notificationService;
            Cliente = clienteService;
        }
    }
}
