using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using AssistenciaTech.Services;

namespace AssistenciaTech.Controllers
{
    /// <summary>
    /// Controller responsável pela Área do Cliente.
    /// Atua como adaptador MVC fino delegando regras de autoatendimento para IAreaClienteEngine.
    /// </summary>
    public class ConsultaController : Controller
    {
        private readonly IAreaClienteEngine _areaClienteEngine;
        private readonly ILogger<ConsultaController> _logger;

        public ConsultaController(IAreaClienteEngine areaClienteEngine, ILogger<ConsultaController> logger)
        {
            _areaClienteEngine = areaClienteEngine ?? throw new ArgumentNullException(nameof(areaClienteEngine));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: /Consulta/
        // Exibe o formulário para o cliente digitar os dados
        public IActionResult Index(int? numeroOS)
        {
            if (numeroOS.HasValue)
            {
                ViewBag.NumeroOS = numeroOS.Value;
            }
            return View();
        }

        // POST: /Consulta/Status
        // Processa os dados enviados pelo formulário
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Status(int numeroOS, string cpf)
        {
            try
            {
                var result = await _areaClienteEngine.ConsultarStatusPublicoAsync(numeroOS, cpf);
                if (!result.Sucesso)
                {
                    ModelState.AddModelError(string.Empty, result.Mensagem ?? "Ordem de Serviço não encontrada ou CPF inválido.");
                    return View("Index");
                }

                return View("Detalhes", result.Ordem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro inesperado ao consultar o status da OS.");
                ModelState.AddModelError(string.Empty, "Ocorreu um erro ao processar sua solicitação. Por favor, tente novamente mais tarde.");
                return View("Index");
            }
        }

        // GET: /Consulta/MeusEquipamentos
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> MeusEquipamentos()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Index");
            }

            var ordens = await _areaClienteEngine.ObterEquipamentosPorEmailAsync(email);
            return View(ordens);
        }

        // GET: /Consulta/VisualizarOS/{id}
        // Visualiza a OS de forma segura apenas se pertencer ao cliente logado
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> VisualizarOS(int id)
        {
            try
            {
                var email = User.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrEmpty(email))
                {
                    return RedirectToAction("Index");
                }

                var result = await _areaClienteEngine.ObterDetalhesSegurosAsync(id, email);
                if (!result.Sucesso)
                {
                    if (result.NaoEncontradoOuNaoAutorizado)
                    {
                        return NotFound(result.Mensagem);
                    }

                    ModelState.AddModelError(string.Empty, result.Mensagem ?? "Erro ao visualizar a OS.");
                    return View("Index");
                }

                return View("Detalhes", result.Ordem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro inesperado ao visualizar a OS.");
                ModelState.AddModelError(string.Empty, "Ocorreu um erro ao processar sua solicitação. Por favor, tente novamente mais tarde.");
                return View("Index");
            }
        }
    }
}
