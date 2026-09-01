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
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AssistenciaTech.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class FaturamentosController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly AssistenciaTech.Services.ITributacaoService _tributacaoService;
        private readonly AssistenciaTech.Services.INfseXmlGeneratorService _xmlGenerator;

        public FaturamentosController(
            AppDbContext context, 
            IConfiguration configuration,
            AssistenciaTech.Services.ITributacaoService tributacaoService,
            AssistenciaTech.Services.INfseXmlGeneratorService xmlGenerator)
        {
            _context = context;
            _configuration = configuration;
            _tributacaoService = tributacaoService;
            _xmlGenerator = xmlGenerator;
        }

        public async Task<IActionResult> Index()
        {
            var faturamentos = await _context.Faturamentos.AsNoTracking().Include(f => f.OrdemServico).ToListAsync();
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

            // Calcular Tributos via Domain Service
            var tributos = _tributacaoService.CalcularTributos(os);

            // Simulação de geração de Payload PIX Dinâmico (BR Code)
            string txId = Guid.NewGuid().ToString("N").Substring(0, 25);
            string qrcodeBase = $"00020101021226580014br.gov.bcb.pix0136{Guid.NewGuid()}5204000053039865405{total.ToString("0.00").Replace(",", ".")}5802BR5915AssistenciaTech6009Sao Paulo62290525{txId}6304ABCD";

            var faturamento = new Faturamento
            {
                OrdemServicoId = osId,
                ValorTotal = total,
                DataVencimento = DateTime.UtcNow.AddDays(3),
                StatusPagamento = PagamentoStatus.Pendente,
                TxIdPix = txId,
                QrCodePayload = qrcodeBase,
                
                // Gravar os impostos desmembrados
                BaseCalculoISS = tributos.BaseCalculoISS,
                AliquotaISS = tributos.AliquotaISS,
                ValorISS = tributos.ValorISS,
                
                BaseCalculoICMS = tributos.BaseCalculoICMS,
                AliquotaICMS = tributos.AliquotaICMS,
                ValorICMS = tributos.ValorICMS
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
            if (string.IsNullOrEmpty(webhookSecret) || webhookSecret.Length < 32)
            {
                // To prevent potential brute-force attacks on HMAC signatures,
                // enforce a minimum secret length (e.g., 32 characters/256 bits).
                return StatusCode(500, "Internal server error: WebhookSecret is missing or too short.");
            }

            if (!Request.Headers.TryGetValue("X-Webhook-Signature", out var providedSignature))
            {
                return Unauthorized("Invalid or missing webhook signature.");
            }

            Request.EnableBuffering();
            Request.Body.Position = 0;
            using var reader = new System.IO.StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            if (!VerifySignature(webhookSecret, providedSignature, payload))
            {
                return Unauthorized("Invalid or missing webhook signature.");
            }

            try
            {
                var txIds = ExtractTxIdsFromJson(payload);

                if (txIds.Count > 0)
                {
                    await ProcessPaymentsAsync(txIds);
                }

                return Ok();
            }
            catch (JsonException)
            {
                return BadRequest("Invalid JSON payload.");
            }
        }

        private bool VerifySignature(string webhookSecret, string providedSignature, string payload)
        {
            byte[] secretBytes = System.Text.Encoding.UTF8.GetBytes(webhookSecret);
            byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
            using var hmac = new HMACSHA256(secretBytes);
            byte[] computedHashBytes = hmac.ComputeHash(payloadBytes);
            string computedSignature = Convert.ToHexString(computedHashBytes).ToLowerInvariant();

            byte[] providedSignatureBytes = System.Text.Encoding.UTF8.GetBytes(providedSignature.ToString());
            byte[] computedSignatureBytes = System.Text.Encoding.UTF8.GetBytes(computedSignature);

            byte[] providedHash = SHA256.HashData(providedSignatureBytes);
            byte[] secretHash = SHA256.HashData(computedSignatureBytes);

            return CryptographicOperations.FixedTimeEquals(providedHash, secretHash);
        }

        private System.Collections.Generic.List<string> ExtractTxIdsFromJson(string payload)
        {
            var txIds = new System.Collections.Generic.List<string>();
            using var jsonDoc = JsonDocument.Parse(payload);

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

        private async Task ProcessPaymentsAsync(System.Collections.Generic.List<string> txIds)
        {
            if (txIds == null || txIds.Count == 0) return;

            if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                var faturamentosToUpdate = await _context.Faturamentos
                    .Where(f => txIds.Contains(f.TxIdPix) && f.StatusPagamento != PagamentoStatus.Pago_Total)
                    .ToListAsync();

                if (faturamentosToUpdate.Count > 0)
                {
                    for (int i = 0; i < faturamentosToUpdate.Count; i++)
                    {
                        faturamentosToUpdate[i].StatusPagamento = PagamentoStatus.Pago_Total;
                    }

                    // Entities are already tracked, no need for _context.UpdateRange which forces all fields to modified
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                await _context.Faturamentos
                    .Where(f => txIds.Contains(f.TxIdPix) && f.StatusPagamento != PagamentoStatus.Pago_Total)
                    .ExecuteUpdateAsync(s => s.SetProperty(f => f.StatusPagamento, PagamentoStatus.Pago_Total));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarPago(int id)
        {
            var affected = await _context.Faturamentos
                .Where(f => f.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.StatusPagamento, PagamentoStatus.Pago_Total));

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Faturamentos/GerarXmlNfse/{id}")]
        public async Task<IActionResult> GerarXmlNfse(int id)
        {
            var faturamento = await _context.Faturamentos
                .Include(f => f.OrdemServico)
                    .ThenInclude(os => os.Cliente)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (faturamento == null) return NotFound("Faturamento não encontrado.");
            if (faturamento.OrdemServico == null || faturamento.OrdemServico.Cliente == null)
                return BadRequest("Dados da OS ou Cliente estão incompletos.");

            var xmlBytes = _xmlGenerator.GerarXml(faturamento);

            return File(xmlBytes, "application/xml", $"Nfse_Fatura_{id}.xml");
        }

        [HttpGet("Faturamentos/GerarReciboPagamento/{id}")]
        public async Task<IActionResult> GerarReciboPagamento(int id)
        {
            var faturamento = await _context.Faturamentos
                .Include(f => f.OrdemServico)
                    .ThenInclude(os => os.Cliente)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (faturamento == null) return NotFound("Faturamento não encontrado.");
            if (faturamento.StatusPagamento != PagamentoStatus.Pago_Total) return BadRequest("Este faturamento ainda não foi pago.");
            if (faturamento.OrdemServico == null || faturamento.OrdemServico.Cliente == null) return BadRequest("Dados da OS ou Cliente incompletos.");

            var os = faturamento.OrdemServico;
            var cliente = os.Cliente;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().Element(ComposeFooter);

                    void ComposeHeader(IContainer container)
                    {
                        container.Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text("RECIBO DE PAGAMENTO").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                                column.Item().Text($"Fatura #{faturamento.Id} | OS #{os.Id}").FontSize(14).FontColor(Colors.Grey.Darken1);
                                column.Item().Text($"Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}");
                            });
                        });
                    }

                    void ComposeContent(IContainer container)
                    {
                        container.PaddingVertical(1, Unit.Centimetre).Column(column =>
                        {
                            column.Spacing(20);

                            column.Item().Text(text =>
                            {
                                text.Span("Recebemos de ").SemiBold();
                                text.Span($"{cliente.Nome} (CPF: {cliente.Cpf}), ").SemiBold();
                                text.Span($"a importância de ");
                                text.Span($"{faturamento.ValorTotal:C}").SemiBold().FontColor(Colors.Green.Darken2);
                                text.Span(" referente ao pagamento integral dos serviços prestados na Ordem de Serviço ");
                                text.Span($"#{os.Id}").SemiBold();
                                text.Span($" ({os.Equipamento}).");
                            });

                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Descrição").SemiBold();
                                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text("Valor").SemiBold();
                                });

                                table.Cell().PaddingVertical(5).Text("Mão de Obra");
                                table.Cell().PaddingVertical(5).AlignRight().Text($"{os.CustoMaoDeObra:C}");

                                table.Cell().PaddingVertical(5).Text("Peças");
                                table.Cell().PaddingVertical(5).AlignRight().Text($"{os.CustoPecas:C}");

                                if (os.DescontoAplicado > 0)
                                {
                                    table.Cell().PaddingVertical(5).Text("Desconto");
                                    table.Cell().PaddingVertical(5).AlignRight().Text($"- {os.DescontoAplicado:C}").FontColor(Colors.Red.Medium);
                                }

                                table.Cell().BorderTop(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(5).Text("Total Pago").SemiBold();
                                table.Cell().BorderTop(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(5).AlignRight().Text($"{faturamento.ValorTotal:C}").SemiBold().FontColor(Colors.Green.Darken2);
                            });

                            column.Item().PaddingTop(40).AlignCenter().Text("__________________________________________________");
                            column.Item().AlignCenter().Text("AssistenciaTech").SemiBold();
                            column.Item().AlignCenter().Text("Assinatura do Recebedor");
                        });
                    }

                    void ComposeFooter(IContainer container)
                    {
                        container.AlignCenter().Text(x =>
                        {
                            x.Span("Gerado por TechOS - AssistenciaTech | Página ");
                            x.CurrentPageNumber();
                            x.Span(" de ");
                            x.TotalPages();
                        });
                    }
                });
            });

            var pdf = document.GeneratePdf();
            return File(pdf, "application/pdf", $"Recibo_Fatura_{id}.pdf");
        }
    }
}
