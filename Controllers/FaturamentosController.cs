using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using Microsoft.AspNetCore.Authorization;

namespace AssistenciaTech.Controllers
{
    [Authorize]
    public class FaturamentosController : Controller
    {
        private readonly AppDbContext _context;

        public FaturamentosController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var faturamentos = await _context.Faturamentos.Include(f => f.OrdemServico).ToListAsync();
            return View(faturamentos);
        }

        // Endpoint para gerar fatura a partir da OS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GerarDaOS(int osId)
        {
            var os = await _context.OrdensServico.FindAsync(osId);
            if (os == null) return NotFound();

            decimal total = (os.CustoPecas + os.CustoMaoDeObra) - os.DescontoAplicado;

            // Simulação de geração de Payload PIX Dinâmico (BR Code)
            string txId = Guid.NewGuid().ToString("N").Substring(0, 25);
            string qrcodeBase = $"00020101021226580014br.gov.bcb.pix0136{Guid.NewGuid()}5204000053039865405{total.ToString("0.00").Replace(",", ".")}5802BR5915AssistenciaTech6009Sao Paulo62290525{txId}6304ABCD";

            var faturamento = new Faturamento
            {
                OrdemServicoId = osId,
                ValorTotal = total,
                DataVencimento = DateTime.Now.AddDays(3),
                StatusPagamento = PagamentoStatus.Pendente,
                TxIdPix = txId,
                QrCodePayload = qrcodeBase
            };

            _context.Faturamentos.Add(faturamento);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Simulação de Webhook (Callback do Banco)
        [HttpPost("api/faturamentos/webhook-pix")]
        [AllowAnonymous]
        public async Task<IActionResult> WebhookPix([FromBody] dynamic payload)
        {
            // Em produção real, este endpoint receberia o JSON do banco informando que o PIX foi pago.
            // Para o MVP, aceitaremos o txId via querystring ou body para simular.
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarPago(int id)
        {
            var faturamento = await _context.Faturamentos.FindAsync(id);
            if (faturamento != null)
            {
                faturamento.StatusPagamento = PagamentoStatus.Pago_Total;
                _context.Update(faturamento);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
