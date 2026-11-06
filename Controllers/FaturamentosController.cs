using System.IO;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssistenciaTech.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class FaturamentosController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IFaturamentoEngine _engine;

        public FaturamentosController(
            AppDbContext context,
            IFaturamentoEngine engine)
        {
            _context = context;
            _engine = engine;
        }

        public async Task<IActionResult> Index()
        {
            var faturamentos = await _context.Faturamentos
                .AsNoTracking()
                .Include(f => f.OrdemServico)
                .ToListAsync();

            return View(faturamentos);
        }

        // Endpoint para gerar fatura a partir da OS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GerarDaOS(int osId)
        {
            var result = await _engine.FaturarOrdemAsync(osId);
            if (!result.Sucesso)
            {
                if (result.Mensagem?.Contains("não encontrada") == true)
                {
                    return NotFound();
                }

                TempData["MensagemErro"] = result.Mensagem;
            }

            return RedirectToAction(nameof(Index));
        }

        // Simulação de Webhook (Callback do Banco)
        [HttpPost("api/faturamentos/webhook-pix")]
        [AllowAnonymous]
        public async Task<IActionResult> WebhookPix()
        {
            Request.EnableBuffering();
            Request.Body.Position = 0;
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            string? signatureHeader = null;
            if (Request.Headers.TryGetValue("X-Webhook-Signature", out var headerVal))
            {
                signatureHeader = headerVal.ToString();
            }

            var result = await _engine.ProcessarWebhookPixAsync(payload, signatureHeader);
            if (!result.Sucesso)
            {
                if (result.HttpStatusCode == StatusCodes.Status401Unauthorized)
                {
                    return Unauthorized(result.MensagemErro);
                }
                if (result.HttpStatusCode == StatusCodes.Status400BadRequest)
                {
                    return BadRequest(result.MensagemErro);
                }
                return StatusCode(result.HttpStatusCode, result.MensagemErro);
            }

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarPago(int id)
        {
            var result = await _engine.LiquidarFaturaManualmenteAsync(id);
            if (!result.Sucesso)
            {
                TempData["MensagemErro"] = result.Mensagem;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Faturamentos/GerarXmlNfse/{id}")]
        public async Task<IActionResult> GerarXmlNfse(int id)
        {
            var result = await _engine.GerarXmlNfseAsync(id);
            if (!result.Sucesso)
            {
                if (result.StatusCode == StatusCodes.Status404NotFound)
                {
                    return NotFound(result.MensagemErro);
                }
                return BadRequest(result.MensagemErro);
            }

            return File(result.ConteudoBytes!, result.ContentType!, result.NomeArquivo);
        }

        [HttpGet("Faturamentos/GerarReciboPagamento/{id}")]
        public async Task<IActionResult> GerarReciboPagamento(int id)
        {
            var result = await _engine.GerarReciboPagamentoPdfAsync(id);
            if (!result.Sucesso)
            {
                if (result.StatusCode == StatusCodes.Status404NotFound)
                {
                    return NotFound(result.MensagemErro);
                }
                return BadRequest(result.MensagemErro);
            }

            return File(result.ConteudoBytes!, result.ContentType!, result.NomeArquivo);
        }
    }
}
