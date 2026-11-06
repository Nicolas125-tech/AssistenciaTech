using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.DTOs;
using AssistenciaTech.Services;
using AssistenciaTech.ViewModels;
using AssistenciaTech.Domain.States;
using System;
using System.Collections.Generic;
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
using Microsoft.Extensions.DependencyInjection;

namespace AssistenciaTech.Controllers
{
    /// <summary>
    /// Controller responsável pelo Painel Administrativo (CRUD de Ordens de Serviço).
    /// </summary>
    [Authorize(Roles = "Administrador")]
    public class AdminController : Controller
    {
        private static readonly string[] _allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };
        private readonly AppDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly IOrdemServicoLifecycle _lifecycle;
        private readonly IAdminDashboardService _dashboard;
        private readonly IPdfGeneratorService _pdfGenerator;
        private readonly IClienteService _clienteService;
        private readonly AssistenciaTech.Extensions.IResilientCacheService _cache;
        private readonly IWebHostEnvironment _env;

        public AdminController(
            AppDbContext context,
            ILogger<AdminController> logger,
            IOrdemServicoLifecycle lifecycle,
            IAdminDashboardService dashboard,
            IPdfGeneratorService pdfGenerator,
            IClienteService clienteService,
            AssistenciaTech.Extensions.IResilientCacheService cache,
            IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _lifecycle = lifecycle;
            _dashboard = dashboard;
            _pdfGenerator = pdfGenerator;
            _clienteService = clienteService;
            _cache = cache;
            _env = env;
        }

        // GET: Admin/Index

        [HttpGet]
        public async Task<IActionResult> ExportarCsv()
        {
            try
            {
                var todasOS = _context.OrdensServico.Include(o => o.Cliente).OrderByDescending(o => o.Id).AsNoTracking().AsAsyncEnumerable();

                Response.Clear();
                Response.ContentType = "text/csv";
                Response.Headers.Append("Content-Disposition", "attachment; filename=\"OrdensDeServico.csv\"");

                await using var streamWriter = new StreamWriter(Response.Body, new System.Text.UTF8Encoding(false));
                await streamWriter.WriteLineAsync("Id,Cliente,Equipamento,Data Entrada,Status,Valor Orçamento");

                var sb = new System.Text.StringBuilder(128 * 100);
                int batchCount = 0;
                await foreach (var os in todasOS)
                {
                    sb.Append(os.Id)
                      .Append(",\"")
                      .Append(os.Cliente?.Nome)
                      .Append("\",\"")
                      .Append(os.Equipamento)
                      .Append("\",")
                      .Append(os.DataEntrada.ToString("dd/MM/yyyy"))
                      .Append(',')
                      .Append(os.Status)
                      .Append(',')
                      .AppendLine(os.ValorOrcamento.ToString(System.Globalization.CultureInfo.InvariantCulture));

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
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB_CONNECTION_ERROR (Admin/ExportarCsv)");
                TempData["ErroBanco"] = "Erro ao exportar os dados. O banco de dados está inacessível.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Index(string? searchString, string? statusFilter, int page = 1)
        {
            var viewModel = new AdminDashboardViewModel
            {
                SearchString = searchString,
                StatusFilter = statusFilter
            };

            if (TempData["ErroBanco"] != null)
            {
                viewModel.ErroBanco = TempData["ErroBanco"]?.ToString();
            }

            try
            {
                viewModel.DashboardData = await _dashboard.GetDashboardDataAsync(searchString, statusFilter, page);
                return View(viewModel);
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "DB_CONNECTION_ERROR (Admin/Index)");
                viewModel.ErroBanco = "Erro ao conectar ao banco de dados. Por favor, tente novamente mais tarde.";
                viewModel.DashboardData = new DashboardDto(); // Return empty dashboard to prevent view errors
                return View(viewModel);
            }
        }

        // GET: Admin/Create
        public async Task<IActionResult> Create()
        {
            try
            {
                // Popula o dropdown de clientes
                await PopulateClientesViewBagAsync();
                return View(new OrdemServicoCreateDto());
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
        public async Task<IActionResult> Create(OrdemServicoCreateDto dto)
        {
            try
            {
                // Validação de segurança básica e ModelState
                if (ModelState.IsValid)
                {
                    var result = await _lifecycle.RegistrarEntradaAsync(dto, User?.Identity?.Name);
                    if (result.Sucesso)
                    {
                        if (!string.IsNullOrEmpty(result.Mensagem) && result.Mensagem.Contains("garantia", StringComparison.OrdinalIgnoreCase))
                        {
                            if (TempData != null)
                            {
                                TempData["AlertaGarantia"] = result.Mensagem;
                            }
                        }
                        return RedirectToAction(nameof(Index));
                    }

                    foreach (var erro in result.Erros)
                    {
                        ModelState.AddModelError(string.Empty, erro);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar a Ordem de Serviço");
                string errorMsg = ex.Message;
                if (ex.InnerException != null) errorMsg = $"{errorMsg} | Inner: {ex.InnerException.Message}";
                ModelState.AddModelError(string.Empty, $"Erro ao salvar a Ordem de Serviço: {errorMsg}");
            }

            // Se falhou, retorna os dados para o formulário
            await PopulateClientesViewBagAsync(dto.ClienteId);
            return View(dto);
        }

        // GET: Admin/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var ordemServico = await _context.OrdensServico
                                                 .AsNoTracking()
                                                 .Include(o => o.Cliente)
                                                 .Include(o => o.TecnicoResponsavel)
                                                 .Include(o => o.EquipamentoBackup)
                                                 .Include(o => o.Evidencias)
                                                 .Include(o => o.Contrato)
                                                 .FirstOrDefaultAsync(m => m.Id == id);

                if (ordemServico == null) return NotFound();

                ViewBag.Tecnicos = new SelectList(await _context.Tecnicos.AsNoTracking().Where(t => t.Ativo).Select(t => new { t.Id, t.Nome }).ToListAsync(), "Id", "Nome");

                var cacheKeyEq = "EquipamentosBackup_All";
                var allEquipamentos = await _cache.GetAsync<System.Collections.Generic.List<EquipamentoBackup>>(cacheKeyEq);
                if (allEquipamentos == null)
                {
                    allEquipamentos = await _context.EquipamentosBackup.AsNoTracking().ToListAsync();
                    await _cache.SetAsync(cacheKeyEq, allEquipamentos, TimeSpan.FromMinutes(60));
                }
                ViewBag.EquipamentosBackup = new SelectList(allEquipamentos.Where(e => e.Disponivel || e.Id == ordemServico.EquipamentoBackupId).Select(e => new { e.Id, e.Descricao }).ToList(), "Id", "Descricao");
                var contratosData = await _context.Contratos.AsNoTracking().Where(c => c.ClienteId == ordemServico.ClienteId).Select(c => new { c.Id, c.HorasSLA, c.ValorMensal }).ToListAsync();
                ViewBag.Contratos = new SelectList(contratosData.Select(c => new { c.Id, NomeDesc = $"Contrato: SLA {c.HorasSLA}h - R$ {c.ValorMensal}" }), "Id", "NomeDesc");

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
        public async Task<IActionResult> Edit(int id, OrdemServico ordemServico, IFormFileCollection? fotos)
        {
            if (id != ordemServico.Id) return NotFound();

            try
            {
                var ordemExistente = await _context.OrdensServico
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (ordemExistente == null) return NotFound();

                var result = await _lifecycle.AtualizarOrdemAsync(id, ordemServico, fotos, User?.Identity?.Name);
                if (!result.Sucesso)
                {
                    foreach (var erro in result.Erros)
                    {
                        ModelState.AddModelError(string.Empty, erro);
                    }
                    await PopulateViewBagsForEditAsync(ordemServico);
                    return View(ordemServico);
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DB_UPDATE_ERROR_EDIT");
                ModelState.AddModelError(string.Empty, "Ocorreu um erro ao atualizar os dados.");
                await PopulateViewBagsForEditAsync(ordemServico);
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
                var ordemServico = await _context.OrdensServico
                    .Include(o => o.Evidencias)
                    .Include(o => o.PecasUtilizadas)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (ordemServico != null)
                {
                    if (ordemServico.Evidencias.Any() || ordemServico.PecasUtilizadas.Any())
                    {
                        return RedirectToAction(nameof(Index), new { erro = "Não é possível excluir a OS pois ela possui dependências (Evidências ou Peças)." });
                    }

                    bool hasFaturamento = await _context.Faturamentos.AnyAsync(f => f.OrdemServicoId == id);
                    if (hasFaturamento)
                    {
                        return RedirectToAction(nameof(Index), new { erro = "Não é possível excluir a OS pois ela possui faturamento." });
                    }

                    _context.OrdensServico.Remove(ordemServico);
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction(nameof(Index));
            }
            catch (System.Data.Common.DbException ex)
            {
                _logger.LogError(ex, "DB_DELETE_ERROR");
                return RedirectToAction(nameof(Index), new { erro = "Não foi possível excluir a OS." });
            }
        }

        // GET: Admin/ImprimirOs/5
        [HttpGet]
        public async Task<IActionResult> ImprimirOs(int id)
        {
            var os = await _context.OrdensServico
                .Include(o => o.Cliente)
                .Include(o => o.PecasUtilizadas)
                    .ThenInclude(p => p.Peca)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (os == null) return NotFound();

            var pdfBytes = _pdfGenerator.GenerateOsPdf(os);

            return File(pdfBytes, "application/pdf", $"OS_{os.Id}_{os.Cliente?.Nome}.pdf");
        }
        [HttpGet]
        public IActionResult GetEvidencia(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return BadRequest();
            }

            var safeFileName = Path.GetFileName(fileName);
            if (string.IsNullOrEmpty(safeFileName) || safeFileName != fileName)
            {
                return BadRequest();
            }

            var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                return BadRequest();
            }

            string uploadsFolder = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "SecureUploads", "Evidencias"));
            string filePath = Path.GetFullPath(Path.Combine(uploadsFolder, safeFileName));

            if (!filePath.StartsWith(uploadsFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest();
            }

            if (!System.IO.File.Exists(filePath))
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

            return PhysicalFile(filePath, contentType);
        }

        private async Task PopulateViewBagsForEditAsync(OrdemServico ordemServico)
        {
            var tecnicos = await _context.Tecnicos.AsNoTracking().Where(t => t.Ativo).ToListAsync();

            var cacheKeyEq = "EquipamentosBackup_All";
            var allEquipamentos = await _cache.GetAsync<System.Collections.Generic.List<EquipamentoBackup>>(cacheKeyEq);
            if (allEquipamentos == null)
            {
                allEquipamentos = await _context.EquipamentosBackup.AsNoTracking().ToListAsync();
                await _cache.SetAsync(cacheKeyEq, allEquipamentos, TimeSpan.FromMinutes(60));
            }
            var equipamentos = allEquipamentos.Where(e => e.Disponivel || e.Id == ordemServico.EquipamentoBackupId).ToList();

            var contratos = await _context.Contratos.Include(c => c.Cliente).AsNoTracking()
                .Where(c => c.ClienteId == ordemServico.ClienteId)
                .Select(c => new { c.Id, NomeDesc = $"Contrato: SLA {c.HorasSLA}h - R$ {c.ValorMensal}" })
                .ToListAsync();

            ViewBag.Tecnicos = new SelectList(tecnicos, "Id", "Nome", ordemServico.TecnicoId);
            ViewBag.EquipamentosBackup = new SelectList(equipamentos, "Id", "Descricao", ordemServico.EquipamentoBackupId);
            ViewBag.Contratos = new SelectList(contratos, "Id", "NomeDesc", ordemServico.ContratoId);
        }

        private async Task PopulateClientesViewBagAsync(int? selectedId = null)
        {
            var selectListItems = await _clienteService.GetClientesSelectListAsync(selectedId);
            ViewBag.Clientes = new SelectList(selectListItems, "Value", "Text", selectedId?.ToString());
        }
    }
}
