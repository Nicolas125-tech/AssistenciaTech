using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AssistenciaTech.Services
{
    public interface IAdminFacade
    {
        IEstoqueService Estoque { get; }
        IWebHostEnvironment Env { get; }
        IPdfGeneratorService PdfGenerator { get; }
        IAdminDashboardService Dashboard { get; }
        IEquipamentoBackupService EquipamentoBackup { get; }
        IServiceScopeFactory ScopeFactory { get; }
        INotificationService Notification { get; }
        IClienteService Cliente { get; }
    }
}
