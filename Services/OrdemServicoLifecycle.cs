using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Domain.States;
using AssistenciaTech.DTOs;
using AssistenciaTech.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssistenciaTech.Services
{
    public class OrdemServicoLifecycle : IOrdemServicoLifecycle
    {
        private readonly AppDbContext _context;
        private readonly IEstoqueEngine _estoqueEngine;
        private readonly INotificationService _notificationService;
        private readonly IEvidenciaStorage _evidenciaStorage;
        private readonly ILogger<OrdemServicoLifecycle> _logger;

        public OrdemServicoLifecycle(
            AppDbContext context,
            IEstoqueEngine estoqueEngine,
            INotificationService notificationService,
            IEvidenciaStorage evidenciaStorage,
            ILogger<OrdemServicoLifecycle> logger)
        {
            _context = context;
            _estoqueEngine = estoqueEngine;
            _notificationService = notificationService;
            _evidenciaStorage = evidenciaStorage;
            _logger = logger;
        }

        public async Task<OrdemServicoResult> RegistrarEntradaAsync(OrdemServicoCreateDto dto, string? usuarioResponsavel = null)
        {
            if (dto == null)
                return OrdemServicoResult.Falha("Dados de entrada não informados.");

            if (dto.ClienteId <= 0)
                return OrdemServicoResult.Falha("O cliente titular é obrigatório.");

            if (string.IsNullOrWhiteSpace(dto.Equipamento))
                return OrdemServicoResult.Falha("A descrição do equipamento é obrigatória.");

            var ordem = new OrdemServico
            {
                ClienteId = dto.ClienteId,
                Equipamento = dto.Equipamento.Trim(),
                NumeroSerie = dto.NumeroSerie?.Trim(),
                ProblemaRelatado = dto.ProblemaRelatado?.Trim() ?? string.Empty,
                Prioridade = dto.Prioridade,
                AvariasPreExistentes = dto.AvariasPreExistentes?.Trim(),
                AnotacoesInternas = dto.AnotacoesInternas?.Trim(),
                CustoPecas = dto.CustoPecas,
                CustoMaoDeObra = dto.CustoMaoDeObra,
                DescontoAplicado = dto.DescontoAplicado,
                ValorOrcamento = (dto.CustoPecas + dto.CustoMaoDeObra) - dto.DescontoAplicado,
                DataEntrada = DateTime.UtcNow,
                Status = WorkflowStatus.Recebido
            };

            _context.OrdensServico.Add(ordem);
            await _context.SaveChangesAsync();

            // Regra: Alerta de Retorno por Número de Série (30 dias)
            string? mensagemGarantia = null;
            if (!string.IsNullOrEmpty(ordem.NumeroSerie))
            {
                var retornoRecente = await _context.OrdensServico
                    .AnyAsync(o => o.NumeroSerie == ordem.NumeroSerie && o.Id != ordem.Id && o.DataEntrada >= DateTime.UtcNow.AddDays(-30));

                if (retornoRecente)
                {
                    mensagemGarantia = $"ATENÇÃO: O equipamento com NS {ordem.NumeroSerie} já deu entrada na assistência nos últimos 30 dias. Verifique se é um retorno em garantia!";
                }
            }

            _logger.LogInformation("Ordem de serviço #{OsId} registrada com sucesso por {Usuario}.", ordem.Id, usuarioResponsavel ?? "sistema");
            return OrdemServicoResult.Ok(ordem, mensagemGarantia ?? "Ordem de serviço registrada com sucesso.");
        }

        public async Task<OrdemServicoResult> AtualizarOrdemAsync(int osId, OrdemServico ordemAtualizada, IFormFileCollection? fotos = null, string? usuarioResponsavel = null)
        {
            var ordemExistente = await _context.OrdensServico
                .Include(o => o.Cliente)
                .Include(o => o.Evidencias)
                .FirstOrDefaultAsync(o => o.Id == osId);

            if (ordemExistente == null)
                return OrdemServicoResult.NaoEncontrado($"Ordem de serviço #{osId} não encontrada.");

            string statusAnterior = ordemExistente.Status;

            // 1. Trata troca de Equipamento de Backup
            await ProcessarTrocaEquipamentoBackupAsync(ordemExistente.EquipamentoBackupId, ordemAtualizada.EquipamentoBackupId);

            // 2. Copia propriedades de bancada
            CopiarDadosBancada(ordemExistente, ordemAtualizada);

            // 3. Validação e execução das regras de transição de status (Workflow)
            var (canProceed, erroDominio) = await ProcessarRegrasTransicaoAsync(ordemExistente, statusAnterior, ordemAtualizada.Status);
            if (!canProceed)
            {
                return OrdemServicoResult.Falha(erroDominio ?? "Transição de status inválida.");
            }

            // 4. Upload de fotos de evidência via seam IEvidenciaStorage
            var uploadedFiles = new List<string>();
            if (fotos != null && fotos.Count > 0)
            {
                uploadedFiles = await ProcessarEvidenciasUploadAsync(ordemExistente, fotos);
            }

            // 5. Persistência atômica no banco de dados (com rollback de arquivos em caso de falha)
            try
            {
                _context.Update(ordemExistente);
                await _context.SaveChangesAsync();
            }
            catch
            {
                foreach (var savedFile in uploadedFiles)
                {
                    await _evidenciaStorage.RemoverAsync(savedFile);
                }
                throw;
            }

            // 6. Notificação pós-commit com isolamento de falha
            if (ordemExistente.Status != statusAnterior && ordemExistente.Cliente != null)
            {
                await DispararNotificacaoSeguraAsync(ordemExistente.Cliente, ordemExistente, statusAnterior);
            }

            _logger.LogInformation("Ordem de serviço #{OsId} atualizada com sucesso ({StatusAnterior} -> {StatusNovo}) por {Usuario}.",
                ordemExistente.Id, statusAnterior, ordemExistente.Status, usuarioResponsavel ?? "sistema");

            return OrdemServicoResult.Ok(ordemExistente, "Ordem de serviço atualizada com sucesso.");
        }

        public async Task<OrdemServicoResult> AvancarStatusAsync(int osId, string novoStatus, string? laudoTecnico = null, string? usuarioResponsavel = null)
        {
            var ordemExistente = await _context.OrdensServico
                .Include(o => o.Cliente)
                .FirstOrDefaultAsync(o => o.Id == osId);

            if (ordemExistente == null)
                return OrdemServicoResult.Falha($"Ordem de serviço #{osId} não encontrada.");

            string statusAnterior = ordemExistente.Status;

            if (!string.IsNullOrWhiteSpace(laudoTecnico))
            {
                ordemExistente.LaudoTecnico = laudoTecnico.Trim();
            }

            var (canProceed, erroDominio) = await ProcessarRegrasTransicaoAsync(ordemExistente, statusAnterior, novoStatus);
            if (!canProceed)
            {
                return OrdemServicoResult.Falha(erroDominio ?? "Transição de status inválida.");
            }

            _context.Update(ordemExistente);
            await _context.SaveChangesAsync();

            if (ordemExistente.Status != statusAnterior && ordemExistente.Cliente != null)
            {
                await DispararNotificacaoSeguraAsync(ordemExistente.Cliente, ordemExistente, statusAnterior);
            }

            _logger.LogInformation("Status da OS #{OsId} avançado para {NovoStatus} por {Usuario}.",
                ordemExistente.Id, novoStatus, usuarioResponsavel ?? "sistema");

            return OrdemServicoResult.Ok(ordemExistente, $"Status atualizado para {novoStatus}.");
        }

        public Task<List<SelecaoItemDto>> ObterClientesParaSelecaoAsync() =>
            _context.Clientes
                .AsNoTracking()
                .Select(c => new SelecaoItemDto(c.Id, $"{c.Nome} - CPF: {c.Cpf} - Tel: {c.Telefone}"))
                .ToListAsync();

        public async Task<OrdemEdicaoDto?> ObterDadosEdicaoAsync(int id)
        {
            var ordem = await _context.OrdensServico
                .AsNoTracking()
                .Include(o => o.Cliente)
                .Include(o => o.TecnicoResponsavel)
                .Include(o => o.EquipamentoBackup)
                .Include(o => o.Evidencias)
                .Include(o => o.Contrato)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (ordem == null)
            {
                return null;
            }

            var lookups = await ObterLookupsEdicaoAsync(ordem.ClienteId, ordem.EquipamentoBackupId);
            return new OrdemEdicaoDto { Ordem = ordem, Lookups = lookups };
        }

        public async Task<OrdemEdicaoLookupsDto> ObterLookupsEdicaoAsync(int clienteId, int? equipamentoBackupId)
        {
            var tecnicos = await _context.Tecnicos
                .AsNoTracking()
                .Where(t => t.Ativo)
                .Select(t => new SelecaoItemDto(t.Id, t.Nome))
                .ToListAsync();

            var equipamentosBackup = await _context.EquipamentosBackup
                .AsNoTracking()
                .Where(e => e.Disponivel || e.Id == equipamentoBackupId)
                .Select(e => new SelecaoItemDto(e.Id, e.Descricao))
                .ToListAsync();

            var contratosRaw = await _context.Contratos
                .AsNoTracking()
                .Where(c => c.ClienteId == clienteId)
                .Select(c => new { c.Id, c.HorasSLA, c.ValorMensal })
                .ToListAsync();

            var contratos = contratosRaw
                .Select(c => new SelecaoItemDto(c.Id, $"Contrato: SLA {c.HorasSLA}h - R$ {c.ValorMensal}"))
                .ToList();

            return new OrdemEdicaoLookupsDto
            {
                Tecnicos = tecnicos,
                EquipamentosBackup = equipamentosBackup,
                Contratos = contratos
            };
        }

        public async Task<OrdemServicoResult> ExcluirOrdemAsync(int id, string? usuarioResponsavel = null)
        {
            var ordem = await _context.OrdensServico
                .Include(o => o.Evidencias)
                .Include(o => o.PecasUtilizadas)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (ordem == null)
            {
                return OrdemServicoResult.NaoEncontrado($"Ordem de serviço #{id} não encontrada.");
            }

            if (ordem.Evidencias.Any() || ordem.PecasUtilizadas.Any())
            {
                return OrdemServicoResult.Falha("Não é possível excluir a OS pois ela possui dependências (Evidências ou Peças).");
            }

            if (await _context.Faturamentos.AnyAsync(f => f.OrdemServicoId == id))
            {
                return OrdemServicoResult.Falha("Não é possível excluir a OS pois ela possui faturamento.");
            }

            if (ordem.EquipamentoBackupId.HasValue)
            {
                return OrdemServicoResult.Falha("Não é possível excluir a OS pois ela possui um Equipamento de Backup alocado. Devolva ou desvincule o backup antes de excluir.");
            }

            _context.OrdensServico.Remove(ordem);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Ordem de serviço #{OsId} excluída com sucesso por {Usuario}.", id, usuarioResponsavel ?? "sistema");
            return OrdemServicoResult.Ok(ordem, "Ordem de serviço excluída com sucesso.");
        }

        public Task<OrdemServico?> ObterOrdemParaImpressaoAsync(int id) =>
            _context.OrdensServico
                .AsNoTracking()
                .Include(o => o.Cliente)
                .Include(o => o.TecnicoResponsavel)
                .Include(o => o.PecasUtilizadas)
                    .ThenInclude(p => p.Peca)
                .FirstOrDefaultAsync(o => o.Id == id);

        public async Task ExportarOrdensCsvAsync(Stream destino, CancellationToken ct = default)
        {
            var todasOS = (
                from os in _context.OrdensServico.AsNoTracking()
                join c in _context.Clientes.AsNoTracking() on os.ClienteId equals c.Id into cj
                from cliente in cj.DefaultIfEmpty()
                orderby os.Id descending
                select new
                {
                    os.Id,
                    ClienteNome = cliente != null ? cliente.Nome : null,
                    os.Equipamento,
                    os.DataEntrada,
                    os.Status,
                    os.ValorOrcamento
                })
                .AsAsyncEnumerable();

            await using var streamWriter = new StreamWriter(destino, new UTF8Encoding(false), leaveOpen: true);
            await streamWriter.WriteLineAsync("Id,Cliente,Equipamento,Data Entrada,Status,Valor Orçamento".AsMemory(), ct);

            var sb = new StringBuilder(128 * 100);
            int batchCount = 0;

            await foreach (var os in todasOS.WithCancellation(ct))
            {
                sb.Append(os.Id)
                  .Append(",\"")
                  .Append(os.ClienteNome)
                  .Append("\",\"")
                  .Append(os.Equipamento)
                  .Append("\",")
                  .Append(os.DataEntrada.ToString("dd/MM/yyyy"))
                  .Append(',')
                  .Append(os.Status)
                  .Append(',')
                  .AppendLine(os.ValorOrcamento.ToString(CultureInfo.InvariantCulture));

                batchCount++;
                if (batchCount >= 100)
                {
                    await streamWriter.WriteAsync(sb, ct);
                    sb.Clear();
                    batchCount = 0;
                }
            }

            if (sb.Length > 0)
            {
                await streamWriter.WriteAsync(sb, ct);
            }
        }

        private async Task ProcessarTrocaEquipamentoBackupAsync(int? equipamentoAntigoId, int? equipamentoNovoId)
        {
            if (equipamentoAntigoId == equipamentoNovoId)
                return;

            if (equipamentoAntigoId is int antigoId)
            {
                var backupAntigo = await _context.EquipamentosBackup.FindAsync(antigoId);
                if (backupAntigo != null) backupAntigo.Disponivel = true;
            }

            if (equipamentoNovoId is int novoId)
            {
                var backupNovo = await _context.EquipamentosBackup.FindAsync(novoId);
                if (backupNovo != null) backupNovo.Disponivel = false;
            }
        }

        private static void CopiarDadosBancada(OrdemServico destino, OrdemServico origem)
        {
            destino.Equipamento = origem.Equipamento;
            destino.NumeroSerie = origem.NumeroSerie;
            destino.ProblemaRelatado = origem.ProblemaRelatado;
            destino.AvariasPreExistentes = origem.AvariasPreExistentes;
            destino.LaudoTecnico = origem.LaudoTecnico;
            destino.AnotacoesInternas = origem.AnotacoesInternas;
            destino.TecnicoId = origem.TecnicoId;
            destino.Prioridade = origem.Prioridade;
            destino.EnviadoParaTerceiro = origem.EnviadoParaTerceiro;
            destino.NomeParceiro = origem.NomeParceiro;
            destino.CustoTerceirizado = origem.CustoTerceirizado;
            destino.PrevisaoRetornoParceiro = origem.PrevisaoRetornoParceiro;
            destino.ContratoId = origem.ContratoId;
            destino.ContadorPaginasInicial = origem.ContadorPaginasInicial;
            destino.ContadorPaginasFinal = origem.ContadorPaginasFinal;
            destino.EquipamentoBackupId = origem.EquipamentoBackupId;
            destino.CustoPecas = origem.CustoPecas;
            destino.CustoMaoDeObra = origem.CustoMaoDeObra;
            destino.DescontoAplicado = origem.DescontoAplicado;
            destino.ValorOrcamento = destino.ValorTotalCalculado;
        }

        private async Task<(bool CanProceed, string? Erro)> ProcessarRegrasTransicaoAsync(OrdemServico ordem, string statusAnterior, string novoStatus)
        {
            ordem.Status = novoStatus;

            // 1. Conclusão da OS -> Baixa de estoque
            if (ordem.Status == WorkflowStatus.Concluido && OrdemServicoStateMachine.ExigeBaixaDeEstoque(statusAnterior, ordem.Status, ordem.DataConclusao))
            {
                ordem.DataConclusao = DateTime.UtcNow;
                var deducaoResult = await _estoqueEngine.DeduzirEstoqueAsync(ordem.Id);
                if (!deducaoResult.Sucesso)
                {
                    ordem.Status = statusAnterior;
                    return (false, deducaoResult.Mensagem);
                }
            }
            // 2. Estorno de Conclusão -> Devolve estoque
            else if (OrdemServicoStateMachine.ExigeEstornoDeEstoque(statusAnterior, ordem.Status))
            {
                ordem.DataConclusao = null;
                var estornoResult = await _estoqueEngine.RestaurarEstoqueAsync(ordem.Id);
                if (!estornoResult.Sucesso)
                {
                    ordem.Status = statusAnterior;
                    return (false, estornoResult.Mensagem);
                }
            }

            // 3. Entrega ao cliente -> Valida devolução do backup
            if (ordem.Status == WorkflowStatus.Entregue && ordem.DataEntregaCliente == null)
            {
                if (ordem.EquipamentoBackupId.HasValue)
                {
                    var backup = await _context.EquipamentosBackup.FindAsync(ordem.EquipamentoBackupId);
                    bool possuiBackupNaoDevolvido = backup != null && !backup.Disponivel;
                    if (!OrdemServicoStateMachine.ValidarTransicao(statusAnterior, ordem.Status, possuiBackupNaoDevolvido, backup?.Descricao, out var erroDominio))
                    {
                        ordem.Status = statusAnterior; // reverte status
                        return (false, erroDominio);
                    }
                }

                ordem.DataEntregaCliente = DateTime.UtcNow;

                if (ordem.DataConclusao == null)
                {
                    ordem.DataConclusao = DateTime.UtcNow;
                    var deducaoResult = await _estoqueEngine.DeduzirEstoqueAsync(ordem.Id);
                    if (!deducaoResult.Sucesso)
                    {
                        ordem.Status = statusAnterior;
                        return (false, deducaoResult.Mensagem);
                    }
                }
            }
            else if (ordem.Status != WorkflowStatus.Entregue)
            {
                ordem.DataEntregaCliente = null;
            }

            return (true, null);
        }

        private async Task<List<string>> ProcessarEvidenciasUploadAsync(OrdemServico ordem, IFormFileCollection fotos)
        {
            var uploadedFiles = new List<string>();
            foreach (var foto in fotos)
            {
                var saveResult = await _evidenciaStorage.SalvarAsync(foto);
                if (!saveResult.Sucesso || string.IsNullOrEmpty(saveResult.CaminhoUrl) || string.IsNullOrEmpty(saveResult.FileName))
                {
                    continue;
                }

                uploadedFiles.Add(saveResult.FileName);
                ordem.Evidencias.Add(new Evidencia
                {
                    OrdemServicoId = ordem.Id,
                    CaminhoArquivo = saveResult.CaminhoUrl,
                    DataUpload = DateTime.UtcNow
                });
            }

            return uploadedFiles;
        }

        private async Task DispararNotificacaoSeguraAsync(Cliente cliente, OrdemServico ordem, string statusAnterior)
        {
            try
            {
                await _notificationService.EnviarNotificacaoStatusAsync(cliente, ordem, statusAnterior);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar notificação de status para OS #{OsId} (Cliente: {ClienteId}).", ordem.Id, cliente.Id);
            }
        }
    }
}
