using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace AssistenciaTech.Controllers
{
    /// <summary>
    /// Controller responsável pelo Painel Administrativo (CRUD de Ordens de Serviço).
    /// </summary>
    [Authorize]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEstoqueService _estoqueService;
        private readonly IWebHostEnvironment _env;
        private readonly IPdfGeneratorService _pdfGeneratorService;
        private readonly IAdminDashboardService _dashboardService;
        private readonly IEquipamentoBackupService _equipamentoBackupService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(AppDbContext context, IEstoqueService estoqueService, IWebHostEnvironment env, IPdfGeneratorService pdfGeneratorService, IAdminDashboardService dashboardService, IEquipamentoBackupService equipamentoBackupService, ILogger<AdminController> logger)
        {
            _context = context;
            _estoqueService = estoqueService;
            _env = env;
            _pdfGeneratorService = pdfGeneratorService;
            _dashboardService = dashboardService;
            _equipamentoBackupService = equipamentoBackupService;
            _logger = logger;
        }

        // GET: Admin/Index
        [HttpGet]
        public async Task ExportarCsv()
        {
            var todasOS = _context.OrdensServico.Include(o => o.Cliente).OrderByDescending(o => o.Id).AsNoTracking().AsAsyncEnumerable();

            Response.Clear();
            Response.ContentType = "text/csv";
            Response.Headers.Append("Content-Disposition", "attachment; filename=\"OrdensDeServico.csv\"");

            await using var streamWriter = new StreamWriter(Response.Body, new System.Text.UTF8Encoding(false));
            await streamWriter.WriteLineAsync("Id,Cliente,Equipamento,Data Entrada,Status,Valor Orçamento");

            var sb = new System.Text.StringBuilder();
            int batchCount = 0;
            await foreach (var os in todasOS)
            {
                sb.Append(os.Id).Append(",\"")
                  .Append(os.Cliente?.Nome).Append("\",\"")
                  .Append(os.Equipamento).Append("\",")
                  .Append(os.DataEntrada.ToString("dd/MM/yyyy")).Append(',')
                  .Append(os.Status).Append(',')
                  .Append(os.ValorOrcamento).AppendLine();

                batchCount++;
                if (batchCount >= 100)
                {
                    await streamWriter.WriteAsync(sb, default);
                    sb.Clear();
                    batchCount = 0;
                }
            }
            if (sb.Length > 0)
            {
                await streamWriter.WriteAsync(sb, default);
            }
        }

        public async Task<IActionResult> Index(string searchString, string statusFilter, int page = 1)
        {
            if (TempData["ErroBanco"] != null)
            {
                ViewBag.ErroBanco = TempData["ErroBanco"].ToString();
            }
            try
            {
                var dashboardData = await _dashboardService.GetDashboardDataAsync(searchString, statusFilter, page);

                ViewBag.SearchString = searchString;
                ViewBag.StatusFilter = statusFilter;

                ViewBag.ChartLabels = dashboardData.ChartLabels;
                ViewBag.ChartData = dashboardData.ChartData;

                // Dashboard Data
                ViewBag.TotalAbertas = dashboardData.TotalAbertas;
                ViewBag.EquipamentosProntos = dashboardData.EquipamentosProntos;
                ViewBag.FaturamentoPrevisto = dashboardData.FaturamentoPrevisto;

                ViewBag.CurrentPage = dashboardData.CurrentPage;
                ViewBag.TotalPages = dashboardData.TotalPages;
                ViewBag.TotalOrdens = dashboardData.TotalOrdens;

                return View(dashboardData.Ordens);
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "DB_CONNECTION_ERROR (Admin/Index)");
                // Log the exception in a real scenario
                ViewBag.ErroBanco = "Erro ao conectar ao banco de dados. Por favor, tente novamente mais tarde.";

                // Zera os valores do Dashboard
                ViewBag.TotalAbertas = 0;
                ViewBag.EquipamentosProntos = 0;
                ViewBag.FaturamentoPrevisto = 0m;

                // Retorna uma lista vazia para a View não quebrar
                return View(new List<OrdemServico>());
            }
        }

        // GET: Admin/Create
        public IActionResult Create()
        {
            try
            {
                // Popula o dropdown de clientes
                ViewBag.Clientes = new SelectList(_context.Clientes.AsNoTracking().Select(c => new { Id = c.Id, Descricao = $"{c.Nome} - CPF: {c.Cpf} - Tel: {c.Telefone}" }), "Id", "Descricao");
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB_CONNECTION_ERROR_CREATE");
                TempData["ErroBanco"] = "Não foi possível carregar a tela de criação. O banco de dados está inacessível.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Admin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrdemServico ordemServico)
        {
            try
            {
                // Validação de segurança básica e ModelState
                if (ModelState.IsValid)
                {
                    ordemServico.DataEntrada = DateTime.Now;
                    ordemServico.Status = WorkflowStatus.Recebido; // Força fluxo inicial
                    ordemServico.ValorOrcamento = ordemServico.ValorTotalCalculado;
                    _context.Add(ordemServico);
                    await _context.SaveChangesAsync();

                    // Regra: Alerta de Retorno por Número de Série (30 dias)
                    if (!string.IsNullOrEmpty(ordemServico.NumeroSerie))
                    {
                        var retornoRecente = await _context.OrdensServico
                            .AnyAsync(o => o.NumeroSerie == ordemServico.NumeroSerie && o.Id != ordemServico.Id && o.DataEntrada >= DateTime.Now.AddDays(-30));

                        if (retornoRecente)
                        {
                            TempData["AlertaGarantia"] = $"ATENÇÃO: O equipamento com NS {ordemServico.NumeroSerie} já deu entrada na assistência nos últimos 30 dias. Verifique se é um retorno em garantia!";
                        }
                    }

                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                // Em um ambiente real, faríamos um log (ex: Serilog)
                string errorMsg = ex.Message;
                if (ex.InnerException != null) errorMsg = $"{errorMsg} | Inner: {ex.InnerException.Message}";
                ModelState.AddModelError(string.Empty, $"Erro ao salvar a Ordem de Serviço: {errorMsg}");
            }

            // Se falhou, retorna os dados para o formulário
            ViewBag.Clientes = new SelectList(_context.Clientes.AsNoTracking().Select(c => new { Id = c.Id, Descricao = $"{c.Nome} - CPF: {c.Cpf} - Tel: {c.Telefone}" }), "Id", "Descricao", ordemServico.ClienteId);
            return View(ordemServico);
        }

        // GET: Admin/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var ordemServico = await _context.OrdensServico
                                                 .Include(o => o.Cliente)
                                                 .Include(o => o.TecnicoResponsavel)
                                                 .Include(o => o.EquipamentoBackup)
                                                 .Include(o => o.Evidencias)
                                                 .Include(o => o.Contrato)
                                                 .FirstOrDefaultAsync(m => m.Id == id);

                if (ordemServico == null) return NotFound();

                ViewBag.Tecnicos = new SelectList(await _context.Tecnicos.Where(t => t.Ativo).ToListAsync(), "Id", "Nome");
                ViewBag.EquipamentosBackup = new SelectList(await _context.EquipamentosBackup.Where(e => e.Disponivel || e.Id == ordemServico.EquipamentoBackupId).ToListAsync(), "Id", "Descricao");
                ViewBag.Contratos = new SelectList(await _context.Contratos.Include(c => c.Cliente).Where(c => c.ClienteId == ordemServico.ClienteId).Select(c => new { c.Id, NomeDesc = "Contrato: SLA " + c.HorasSLA + "h - R$ " + c.ValorMensal }).ToListAsync(), "Id", "NomeDesc");

                return View(ordemServico);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB_CONNECTION_ERROR_EDIT");
                TempData["ErroBanco"] = "Não foi possível carregar a tela de edição. O banco de dados está inacessível.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Admin/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OrdemServico ordemServico, IFormFileCollection fotos)
        {
            if (id != ordemServico.Id) return NotFound();

            try
            {
                var ordemExistente = await _context.OrdensServico
                    .Include(o => o.Evidencias)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (ordemExistente == null) return NotFound();

                string statusAnterior = ordemExistente.Status;

                await _equipamentoBackupService.ProcessarTrocaEquipamentoAsync(ordemExistente.EquipamentoBackupId, ordemServico.EquipamentoBackupId);

                UpdateOrdemServicoProperties(ordemExistente, ordemServico);

                bool canProceed = await ProcessWorkflowRulesAsync(ordemExistente, statusAnterior, ordemServico);
                if (!canProceed)
                {
                    await PopulateViewBagsForEditAsync(ordemServico);
                    return View(ordemExistente);
                }

                await ProcessEvidenciaUploadsAsync(ordemExistente, fotos);

                _context.Update(ordemExistente);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB_UPDATE_ERROR_EDIT");
                ModelState.AddModelError(string.Empty, "Ocorreu um erro ao atualizar os dados.");
                return View(ordemServico);
            }
        }

        // POST: Admin/Delete/5
        // Neste formato não teremos uma View dedicada para o Delete,
        // o post virá direto da Index por um botão de confirmação.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var ordemServico = await _context.OrdensServico.FindAsync(id);
                if (ordemServico != null)
                {
                    _context.OrdensServico.Remove(ordemServico);
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB_DELETE_ERROR");
                // Tratar caso a OS tenha dependências impeditivas (raro neste escopo)
                return RedirectToAction(nameof(Index), new { erro = "Não foi possível excluir a OS." });
            }
        }

        // GET: Admin/ImprimirOs/5
        [HttpGet]
        public async Task<IActionResult> ImprimirOs(int id)
        {
            var os = await _context.OrdensServico.Include(o => o.Cliente).FirstOrDefaultAsync(o => o.Id == id);

            if (os == null) return NotFound();

            var pdfBytes = _pdfGeneratorService.GenerateOsPdf(os);

            return File(pdfBytes, "application/pdf", $"OS_{os.Id}_{os.Cliente?.Nome}.pdf");
        }
        [HttpGet]
        public IActionResult GetEvidencia(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            {
                return BadRequest();
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };
            if (!allowedExtensions.Contains(extension) || Path.GetFileName(fileName) != fileName)
            {
                return BadRequest();
            }

            string uploadsFolder = Path.Combine(_env.ContentRootPath, "SecureUploads", "Evidencias");
            string filePath = Path.Combine(uploadsFolder, fileName);

            var fullUploadsFolder = Path.GetFullPath(uploadsFolder);
            if (!fullUploadsFolder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                fullUploadsFolder += Path.DirectorySeparatorChar;
            }

            var fullFilePath = Path.GetFullPath(filePath);
            if (!fullFilePath.StartsWith(fullUploadsFolder, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest();
            }

            if (!System.IO.File.Exists(fullFilePath))
            {
                return NotFound();
            }

            string contentType = extension switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };

            return PhysicalFile(fullFilePath, contentType);
        }

        private static async Task<bool> IsValidFileSignatureAsync(IFormFile file, string extension)
        {
            if (file == null || file.Length == 0) return false;

            await using var stream = file.OpenReadStream();
            var signatures = new Dictionary<string, List<byte[]>>
            {
                { ".jpg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } } },
                { ".jpeg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } } },
                { ".png", new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
                { ".gif", new List<byte[]> { new byte[] { 0x47, 0x49, 0x46, 0x38 } } },
                { ".pdf", new List<byte[]> { new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D } } }
            };

            if (!signatures.TryGetValue(extension, out var expectedSignatures))
                return false;

            var maxSignatureLength = expectedSignatures.Max(s => s.Length);
            var headerBytes = new byte[maxSignatureLength];

            int bytesRead = await stream.ReadAsync(headerBytes, 0, maxSignatureLength);
            if (bytesRead < maxSignatureLength && bytesRead < expectedSignatures.Min(s => s.Length))
            {
                return false;
            }

            return expectedSignatures.Any(signature =>
                headerBytes.Take(signature.Length).SequenceEqual(signature));
        }


        private void UpdateOrdemServicoProperties(OrdemServico ordemExistente, OrdemServico ordemServico)
        {
            ordemExistente.Equipamento = ordemServico.Equipamento;
            ordemExistente.NumeroSerie = ordemServico.NumeroSerie;
            ordemExistente.ProblemaRelatado = ordemServico.ProblemaRelatado;
            ordemExistente.AvariasPreExistentes = ordemServico.AvariasPreExistentes;
            ordemExistente.LaudoTecnico = ordemServico.LaudoTecnico;
            ordemExistente.TecnicoId = ordemServico.TecnicoId;
            ordemExistente.EnviadoParaTerceiro = ordemServico.EnviadoParaTerceiro;
            ordemExistente.NomeParceiro = ordemServico.NomeParceiro;
            ordemExistente.CustoTerceirizado = ordemServico.CustoTerceirizado;
            ordemExistente.PrevisaoRetornoParceiro = ordemServico.PrevisaoRetornoParceiro;
            ordemExistente.ContratoId = ordemServico.ContratoId;
            ordemExistente.ContadorPaginasInicial = ordemServico.ContadorPaginasInicial;
            ordemExistente.ContadorPaginasFinal = ordemServico.ContadorPaginasFinal;
            ordemExistente.EquipamentoBackupId = ordemServico.EquipamentoBackupId;
            ordemExistente.Status = ordemServico.Status;
            ordemExistente.CustoPecas = ordemServico.CustoPecas;
            ordemExistente.CustoMaoDeObra = ordemServico.CustoMaoDeObra;
            ordemExistente.DescontoAplicado = ordemServico.DescontoAplicado;
            ordemExistente.ValorOrcamento = ordemExistente.ValorTotalCalculado;
        }

        private async Task<bool> ProcessWorkflowRulesAsync(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemServico)
        {
            if (ordemExistente.Status == WorkflowStatus.Concluido && ordemExistente.DataConclusao == null)
            {
                ordemExistente.DataConclusao = DateTime.Now;
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

                ordemExistente.DataEntregaCliente = DateTime.Now;

                if (ordemExistente.DataConclusao == null)
                {
                    ordemExistente.DataConclusao = DateTime.Now;
                    await _estoqueService.DeduzirEstoque(ordemExistente.Id);
                }
            }
            else if (ordemExistente.Status != WorkflowStatus.Entregue)
            {
                ordemExistente.DataEntregaCliente = null;
            }
            return true;
        }

        private async Task PopulateViewBagsForEditAsync(OrdemServico ordemServico)
        {
            ViewBag.Tecnicos = new SelectList(await _context.Tecnicos.Where(t => t.Ativo).ToListAsync(), "Id", "Nome", ordemServico.TecnicoId);
            ViewBag.EquipamentosBackup = new SelectList(await _context.EquipamentosBackup.Where(e => e.Disponivel || e.Id == ordemServico.EquipamentoBackupId).ToListAsync(), "Id", "Descricao", ordemServico.EquipamentoBackupId);
            ViewBag.Contratos = new SelectList(await _context.Contratos.Include(c => c.Cliente).Where(c => c.ClienteId == ordemServico.ClienteId).Select(c => new { c.Id, NomeDesc = "Contrato: SLA " + c.HorasSLA + "h - R$ " + c.ValorMensal }).ToListAsync(), "Id", "NomeDesc", ordemServico.ContratoId);
        }

        private async Task ProcessEvidenciaUploadsAsync(OrdemServico ordemExistente, IFormFileCollection fotos)
        {
            if (fotos != null && fotos.Count > 0)
            {
                string uploadsFolder = Path.Combine(_env.ContentRootPath, "SecureUploads", "Evidencias");
                Directory.CreateDirectory(uploadsFolder);
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };

                var uploadTasks = new List<Task>();

                foreach (var foto in fotos)
                {
                    if (foto.Length > 0)
                    {
                        var extension = Path.GetExtension(foto.FileName).ToLowerInvariant();
                        if (!allowedExtensions.Contains(extension))
                        {
                            continue;
                        }

                        if (!await IsValidFileSignatureAsync(foto, extension))
                        {
                            continue;
                        }

                        string uniqueFileName = $"{Guid.NewGuid()}{extension}";
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        var currentFoto = foto;
                        async Task SaveFileAsync()
                        {
                            await using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await currentFoto.CopyToAsync(fileStream);
                            }
                        }

                        uploadTasks.Add(SaveFileAsync());

                        ordemExistente.Evidencias.Add(new Evidencia
                        {
                            CaminhoArquivo = $"/Admin/GetEvidencia?fileName={uniqueFileName}",
                            DataUpload = DateTime.Now
                        });
                    }
                }

                await Task.WhenAll(uploadTasks);
            }
        }
    }
}
