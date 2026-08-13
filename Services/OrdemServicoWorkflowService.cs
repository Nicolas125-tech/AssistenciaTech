using System;
using System.Threading.Tasks;
using AssistenciaTech.Models;
using AssistenciaTech.Data;

namespace AssistenciaTech.Services
{
    public class OrdemServicoWorkflowService : IOrdemServicoWorkflowService
    {
        private readonly IEstoqueService _estoqueService;
        private readonly AppDbContext _context;
        private readonly IEquipamentoBackupService _equipamentoBackupService;

        public OrdemServicoWorkflowService(
            IEstoqueService estoqueService,
            AppDbContext context,
            IEquipamentoBackupService equipamentoBackupService)
        {
            _estoqueService = estoqueService;
            _context = context;
            _equipamentoBackupService = equipamentoBackupService;
        }

        public async Task<(bool Success, string ErrorMessage)> ProcessWorkflowAsync(OrdemServico ordemExistente, OrdemServico ordemNova, string statusAnterior)
        {
            if (ordemExistente.Status == WorkflowStatus.Concluido && ordemExistente.DataConclusao == null)
            {
                ordemExistente.DataConclusao = DateTime.Now;
                await _estoqueService.DeduzirEstoque(ordemExistente.Id);
            }
            else if (statusAnterior == WorkflowStatus.Concluido && ordemExistente.Status != WorkflowStatus.Concluido && ordemExistente.Status != WorkflowStatus.Entregue)
            {
                ordemExistente.DataConclusao = null; // Caso retroceda
                await _estoqueService.RestaurarEstoque(ordemExistente.Id);
            }

            if (ordemExistente.Status == WorkflowStatus.Entregue && ordemExistente.DataEntregaCliente == null)
            {
                // BLOQUEIO EMPRESARIAL: Equipamento de Backup precisa ser devolvido primeiro
                if (ordemExistente.EquipamentoBackupId.HasValue)
                {
                    var backup = await _context.EquipamentosBackup.FindAsync(ordemExistente.EquipamentoBackupId);
                    if (backup != null && backup.Disponivel == false)
                    {
                        return (false, $"O status não pode ser 'Entregue' até que o equipamento '{backup.Descricao}' seja devolvido no sistema.");
                    }
                }

                // A garantia passa a valer a partir deste momento
                ordemExistente.DataEntregaCliente = DateTime.Now;

                // Garante que a data de conclusão também exista se pular direto
                if (ordemExistente.DataConclusao == null)
                {
                    ordemExistente.DataConclusao = DateTime.Now;
                    await _estoqueService.DeduzirEstoque(ordemExistente.Id);
                }
            }
            else if (ordemExistente.Status != WorkflowStatus.Entregue)
            {
                ordemExistente.DataEntregaCliente = null; // Anula garantia se retroceder
            }

            return (true, string.Empty);
        }
    }
}
