using System.Threading.Tasks;
using AssistenciaTech.Models;

namespace AssistenciaTech.Services
{
    public interface IOrdemServicoWorkflowService
    {
        Task<(bool Success, string ErrorMessage)> ProcessWorkflowAsync(OrdemServico ordemExistente, OrdemServico ordemNova, string statusAnterior);
    }
}
