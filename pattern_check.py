import re

code = """
        private async Task<bool> ProcessWorkflowRulesAsync(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemServico)
        {
            if (ordemExistente.Status == WorkflowStatus.Concluido && ordemExistente.DataConclusao == null)
            {
                ordemExistente.DataConclusao = DateTime.UtcNow;
                await _estoqueService.DeduzirEstoque(ordemExistente.Id);
            }
            else if (statusAnterior == WorkflowStatus.Concluido && ordemExistente.Status != WorkflowStatus.Concluido && ordemExistente.Status != WorkflowStatus.Entregue)
            {
                ordemExistente.DataConclusao = null;
                await _estoqueService.RestaurarEstoque(ordemExistente.Id);
            }

            if (ordemExistente.Status == WorkflowStatus.Entregue && ordemExistente.DataEntregaCliente == null)
            {
                if (ordemExistente.EquipamentoBackupId.HasValue)
                {
                    var backup = await _context.EquipamentosBackup.FindAsync(ordemExistente.EquipamentoBackupId);
                    if (backup != null && backup.Disponivel == false)
                    {
                        ModelState.AddModelError(string.Empty, $"O status não pode ser 'Entregue' até que o equipamento '{backup.Descricao}' seja devolvido no sistema.");
                        return false;
                    }
                }

                ordemExistente.DataEntregaCliente = DateTime.UtcNow;

                if (ordemExistente.DataConclusao == null)
                {
                    ordemExistente.DataConclusao = DateTime.UtcNow;
                    await _estoqueService.DeduzirEstoque(ordemExistente.Id);
                }
            }
            else if (ordemExistente.Status != WorkflowStatus.Entregue)
            {
                ordemExistente.DataEntregaCliente = null;
            }
            return true;
        }
"""
