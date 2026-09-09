using System.Threading.Tasks;
using AssistenciaTech.Models;
using AssistenciaTech.Data;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AssistenciaTech.Services.Workflow
{
    public interface IWorkflowRule
    {
        bool CanProcess(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova);
        Task<(bool Success, string? ErrorMessage)> ProcessAsync(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova, AppDbContext context);
    }
}
