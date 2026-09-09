using System;
using System.Threading.Tasks;
using AssistenciaTech.Models;
using AssistenciaTech.Data;

namespace AssistenciaTech.Services.Workflow
{
    public class ConcluidoRule : IWorkflowRule
    {
        private readonly IEstoqueService _estoqueService;

        public ConcluidoRule(IEstoqueService estoqueService)
        {
            _estoqueService = estoqueService;
        }

        public bool CanProcess(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova)
        {
            return ordemExistente.Status == WorkflowStatus.Concluido && ordemExistente.DataConclusao == null;
        }

        public async Task<(bool Success, string? ErrorMessage)> ProcessAsync(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova, AppDbContext context)
        {
            ordemExistente.DataConclusao = DateTime.UtcNow;
            await _estoqueService.DeduzirEstoque(ordemExistente.Id);
            return (true, null);
        }
    }

    public class UndoConcluidoRule : IWorkflowRule
    {
        private readonly IEstoqueService _estoqueService;

        public UndoConcluidoRule(IEstoqueService estoqueService)
        {
            _estoqueService = estoqueService;
        }

        public bool CanProcess(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova)
        {
            return statusAnterior == WorkflowStatus.Concluido &&
                   ordemExistente.Status != WorkflowStatus.Concluido &&
                   ordemExistente.Status != WorkflowStatus.Entregue;
        }

        public async Task<(bool Success, string? ErrorMessage)> ProcessAsync(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova, AppDbContext context)
        {
            ordemExistente.DataConclusao = null;
            await _estoqueService.RestaurarEstoque(ordemExistente.Id);
            return (true, null);
        }
    }

    public class EntregueRule : IWorkflowRule
    {
        private readonly IEstoqueService _estoqueService;

        public EntregueRule(IEstoqueService estoqueService)
        {
            _estoqueService = estoqueService;
        }

        public bool CanProcess(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova)
        {
            return ordemExistente.Status == WorkflowStatus.Entregue && ordemExistente.DataEntregaCliente == null;
        }

        public async Task<(bool Success, string? ErrorMessage)> ProcessAsync(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova, AppDbContext context)
        {
            if (ordemExistente.EquipamentoBackupId.HasValue)
            {
                var backup = await context.EquipamentosBackup.FindAsync(ordemExistente.EquipamentoBackupId);
                if (backup != null && backup.Disponivel == false)
                {
                    return (false, $"O status não pode ser 'Entregue' até que o equipamento '{backup.Descricao}' seja devolvido no sistema.");
                }
            }

            ordemExistente.DataEntregaCliente = DateTime.UtcNow;

            if (ordemExistente.DataConclusao == null)
            {
                ordemExistente.DataConclusao = DateTime.UtcNow;
                await _estoqueService.DeduzirEstoque(ordemExistente.Id);
            }

            return (true, null);
        }
    }

    public class UndoEntregueRule : IWorkflowRule
    {
        public bool CanProcess(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova)
        {
            return ordemExistente.Status != WorkflowStatus.Entregue;
        }

        public Task<(bool Success, string? ErrorMessage)> ProcessAsync(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova, AppDbContext context)
        {
            ordemExistente.DataEntregaCliente = null;
            return Task.FromResult((true, (string?)null));
        }
    }
}
