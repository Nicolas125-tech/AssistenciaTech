using System.Threading.Tasks;
using AssistenciaTech.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AssistenciaTech.Services.Workflow
{
    public interface IWorkflowProcessor
    {
        Task<bool> ProcessAllAsync(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova, ModelStateDictionary modelState);
    }
}
