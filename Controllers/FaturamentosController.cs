using System;
using System.Security.Cryptography;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace AssistenciaTech.Controllers
{
    [Authorize]
    public class FaturamentosController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public FaturamentosController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
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
        public async Task<IActionResult> WebhookPix()
        {
            var webhookSecret = _configuration["WebhookSecret"];
            if (string.IsNullOrEmpty(webhookSecret))
            {
                return StatusCode(500, "Internal server error.");
            }

            if (!Request.Headers.TryGetValue("X-Webhook-Signature", out var providedSignature))
            {
                return Unauthorized("Invalid or missing webhook signature.");
            }

            // Read the request body
            Request.EnableBuffering();
            Request.Body.Position = 0;
            using var reader = new System.IO.StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();
            Request.Body.Position = 0; // Reset for potential downstream readers

            if (!IsSignatureValid(payload, providedSignature.ToString(), webhookSecret))
            {
                return Unauthorized("Invalid or missing webhook signature.");
            }

            try
            {
                using var jsonDoc = JsonDocument.Parse(payload);
                var txIds = ExtractTxIds(jsonDoc);

                if (txIds.Count > 0)
                {
                    await ProcessarPagamentosPixAsync(txIds);
                }

                return Ok();
            }
            catch (JsonException)
            {
                return BadRequest("Invalid JSON payload.");
            }
        }

        private bool IsSignatureValid(string payload, string providedSignature, string webhookSecret)
        {
            byte[] secretBytes = System.Text.Encoding.UTF8.GetBytes(webhookSecret);
            byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
            using var hmac = new HMACSHA256(secretBytes);
            byte[] computedHashBytes = hmac.ComputeHash(payloadBytes);
            string computedSignature = Convert.ToHexString(computedHashBytes).ToLowerInvariant();

            byte[] providedSignatureBytes = System.Text.Encoding.UTF8.GetBytes(providedSignature);
            byte[] computedSignatureBytes = System.Text.Encoding.UTF8.GetBytes(computedSignature);

            byte[] providedHash = SHA256.HashData(providedSignatureBytes);
            byte[] secretHash = SHA256.HashData(computedSignatureBytes);

            return CryptographicOperations.FixedTimeEquals(providedHash, secretHash);
        }

        private System.Collections.Generic.List<string> ExtractTxIds(JsonDocument jsonDoc)
        {
            var txIds = new System.Collections.Generic.List<string>();

            // Try BACEN standard format (array of pix objects)
            if (jsonDoc.RootElement.TryGetProperty("pix", out var pixElement) && pixElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var pix in pixElement.EnumerateArray())
                {
                    if (pix.TryGetProperty("txid", out var txidElement))
                    {
                        string txId = txidElement.GetString();
                        if (!string.IsNullOrEmpty(txId))
                        {
                            txIds.Add(txId);
                        }
                    }
                }
            }
            // Fallback to simple format
            else if (jsonDoc.RootElement.TryGetProperty("txid", out var txidElement))
            {
                string txId = txidElement.GetString();
                if (!string.IsNullOrEmpty(txId))
                {
                    txIds.Add(txId);
                }
            }

            return txIds;
        }

        private async Task ProcessarPagamentosPixAsync(System.Collections.Generic.List<string> txIds)
        {
            // Fetch only faturamentos that match the txids and are not already paid
            var faturamentosToUpdate = await _context.Faturamentos
                .Where(f => txIds.Contains(f.TxIdPix) && f.StatusPagamento != PagamentoStatus.Pago_Total)
                .ToListAsync();

            if (faturamentosToUpdate.Count > 0)
            {
                foreach (var faturamento in faturamentosToUpdate)
                {
                    faturamento.StatusPagamento = PagamentoStatus.Pago_Total;
                }

                _context.UpdateRange(faturamentosToUpdate);
                await _context.SaveChangesAsync();
            }
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
