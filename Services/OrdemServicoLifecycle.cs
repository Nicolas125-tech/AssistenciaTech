using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Domain.States;
using AssistenciaTech.DTOs;
using AssistenciaTech.Models;
using Microsoft.AspNetCore.Hosting;
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
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<OrdemServicoLifecycle> _logger;

        private static readonly HashSet<string> _allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".pdf"
        };

        private static readonly Dictionary<string, List<byte[]>> _fileSignatures = new()
        {
            { ".jpg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } } },
            { ".jpeg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } } },
            { ".png", new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
            { ".gif", new List<byte[]> { new byte[] { 0x47, 0x49, 0x46, 0x38 } } },
            { ".pdf", new List<byte[]> { new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D } } }
        };

        public OrdemServicoLifecycle(
            AppDbContext context,
            IEstoqueEngine estoqueEngine,
            INotificationService notificationService,
            IWebHostEnvironment env,
            ILogger<OrdemServicoLifecycle> logger)
        {
            _context = context;
            _estoqueEngine = estoqueEngine;
            _notificationService = notificationService;
            _env = env;
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
                return OrdemServicoResult.Falha($"Ordem de serviço #{osId} não encontrada.");

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

            // 4. Upload de fotos de evidência se houver
            if (fotos != null && fotos.Count > 0)
            {
                await ProcessarEvidenciasUploadAsync(ordemExistente, fotos);
            }

            // 5. Persistência atômica no banco de dados
            _context.Update(ordemExistente);
            await _context.SaveChangesAsync();

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

        private async Task ProcessarEvidenciasUploadAsync(OrdemServico ordem, IFormFileCollection fotos)
        {
            string contentRoot = _env.ContentRootPath ?? AppDomain.CurrentDomain.BaseDirectory;
            string uploadsFolder = Path.Combine(contentRoot, "SecureUploads", "Evidencias");
            Directory.CreateDirectory(uploadsFolder);

            foreach (var foto in fotos)
            {
                if (foto.Length == 0) continue;

                var extension = Path.GetExtension(foto.FileName).ToLowerInvariant();
                if (!_allowedExtensions.Contains(extension)) continue;

                if (!await IsValidFileSignatureAsync(foto, extension)) continue;

                string uniqueFileName = $"{Guid.NewGuid()}{extension}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                await using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await foto.CopyToAsync(fileStream);
                }

                var evidencia = new Evidencia
                {
                    OrdemServicoId = ordem.Id,
                    CaminhoArquivo = $"/Admin/DownloadEvidencia/{uniqueFileName}",
                    DataUpload = DateTime.UtcNow
                };

                ordem.Evidencias.Add(evidencia);
            }
        }

        private static async Task<bool> IsValidFileSignatureAsync(IFormFile file, string extension)
        {
            if (file == null || file.Length == 0) return false;

            await using var stream = file.OpenReadStream();

            if (!_fileSignatures.TryGetValue(extension, out var expectedSignatures))
                return false;

            var maxSignatureLength = expectedSignatures.Max(s => s.Length);
            var headerBytes = new byte[maxSignatureLength];

            int bytesRead = await stream.ReadAsync(headerBytes.AsMemory(0, maxSignatureLength));
            if (bytesRead < maxSignatureLength && bytesRead < expectedSignatures.Min(s => s.Length))
            {
                return false;
            }

            return expectedSignatures.Any(signature =>
                headerBytes.Take(signature.Length).SequenceEqual(signature));
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
