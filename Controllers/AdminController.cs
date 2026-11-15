using System;
using System.Data.Common;
using System.Threading.Tasks;
using AssistenciaTech.DTOs;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using AssistenciaTech.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;

namespace AssistenciaTech.Controllers
{
    /// <summary>
    /// Controller responsável pelo Painel Administrativo (adaptador HTTP para o backoffice de Ordens de Serviço).
    /// </summary>
    [Authorize(Roles = "Administrador")]
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private readonly IOrdemServicoLifecycle _lifecycle;
        private readonly IAdminDashboardService _dashboard;
        private readonly IPdfGeneratorService _pdfGenerator;
        private readonly IEvidenciaStorage _evidenciaStorage;

        public AdminController(
            ILogger<AdminController> logger,
            IOrdemServicoLifecycle lifecycle,
            IAdminDashboardService dashboard,
            IPdfGeneratorService pdfGenerator,
            IEvidenciaStorage evidenciaStorage)
        {
            _logger = logger;
            _lifecycle = lifecycle;
            _dashboard = dashboard;
            _pdfGenerator = pdfGenerator;
            _evidenciaStorage = evidenciaStorage;
        }

        [HttpGet]
        public async Task<IActionResult> ExportarCsv()
        {
            try
            {
                Response.Clear();
                Response.ContentType = "text/csv";
                Response.Headers.Append("Content-Disposition", "attachment; filename=\"OrdensDeServico.csv\"");

                await _lifecycle.ExportarOrdensCsvAsync(Response.Body, HttpContext?.RequestAborted ?? default);
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
                viewModel.DashboardData = new DashboardDto();
                return View(viewModel);
            }
        }

        // GET: Admin/Create
        public async Task<IActionResult> Create()
        {
            try
            {
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

            await PopulateClientesViewBagAsync(dto.ClienteId);
            return View(dto);
        }

        // GET: Admin/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var dadosEdicao = await _lifecycle.ObterDadosEdicaoAsync(id.Value);
                if (dadosEdicao == null) return NotFound();

                AplicarLookupsEdicao(dadosEdicao.Lookups, dadosEdicao.Ordem);
                return View(dadosEdicao.Ordem);
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
                var result = await _lifecycle.AtualizarOrdemAsync(id, ordemServico, fotos, User?.Identity?.Name);
                if (result.NaoEncontrada) return NotFound();

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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _lifecycle.ExcluirOrdemAsync(id, User?.Identity?.Name);
                if (!result.Sucesso && !result.NaoEncontrada)
                {
                    return RedirectToAction(nameof(Index), new { erro = result.Mensagem });
                }

                return RedirectToAction(nameof(Index));
            }
            catch (DbException ex)
            {
                _logger.LogError(ex, "DB_DELETE_ERROR");
                return RedirectToAction(nameof(Index), new { erro = "Não foi possível excluir a OS." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao excluir a OS {Id}.", id);
                TempData["ErrorMessage"] = "Ocorreu um erro ao tentar excluir a Ordem de Serviço.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Admin/ImprimirOs/5
        [HttpGet]
        public async Task<IActionResult> ImprimirOs(int id)
        {
            var os = await _lifecycle.ObterOrdemParaImpressaoAsync(id);
            if (os == null) return NotFound();

            var pdfBytes = _pdfGenerator.GenerateOsPdf(os);
            return File(pdfBytes, "application/pdf", $"OS_{os.Id}_{os.Cliente?.Nome}.pdf");
        }

        // Rota canônica /Admin/GetEvidencia?fileName=... e alias legado /Admin/DownloadEvidencia/{fileName}
        [HttpGet("/Admin/GetEvidencia")]
        [HttpGet("/Admin/DownloadEvidencia/{fileName}")]
        public async Task<IActionResult> GetEvidencia(string? fileName)
        {
            var result = await _evidenciaStorage.AbrirLeituraAsync(fileName);
            return result.Status switch
            {
                EvidenciaReadStatus.Invalido => BadRequest(),
                EvidenciaReadStatus.NaoEncontrado => NotFound(),
                EvidenciaReadStatus.Ok when result.Conteudo != null && !string.IsNullOrEmpty(result.ContentType)
                    => File(result.Conteudo, result.ContentType),
                _ => NotFound()
            };
        }

        private async Task PopulateViewBagsForEditAsync(OrdemServico ordemServico)
        {
            var lookups = await _lifecycle.ObterLookupsEdicaoAsync(ordemServico.ClienteId, ordemServico.EquipamentoBackupId);
            AplicarLookupsEdicao(lookups, ordemServico);
        }

        private void AplicarLookupsEdicao(OrdemEdicaoLookupsDto lookups, OrdemServico ordemServico)
        {
            ViewBag.Tecnicos = new SelectList(lookups.Tecnicos, "Id", "Descricao", ordemServico.TecnicoId);
            ViewBag.EquipamentosBackup = new SelectList(lookups.EquipamentosBackup, "Id", "Descricao", ordemServico.EquipamentoBackupId);
            ViewBag.Contratos = new SelectList(lookups.Contratos, "Id", "Descricao", ordemServico.ContratoId);
        }

        private async Task PopulateClientesViewBagAsync(int? selectedId = null)
        {
            var clientes = await _lifecycle.ObterClientesParaSelecaoAsync();
            ViewBag.Clientes = new SelectList(clientes, "Id", "Descricao", selectedId?.ToString());
        }
    }
}
