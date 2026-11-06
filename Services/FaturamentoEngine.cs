using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AssistenciaTech.Services
{
    public class FaturamentoEngine : IFaturamentoEngine
    {
        private const decimal AliquotaIssPadrao = 0.05m; // 5% sobre mão de obra
        private const decimal AliquotaIcmsPadrao = 0.18m; // 18% sobre peças
        private const int MinimoCaracteresSecret = 32;

        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IPdfGeneratorService _pdfGenerator;
        private readonly ILogger<FaturamentoEngine> _logger;

        public FaturamentoEngine(
            AppDbContext context,
            IConfiguration configuration,
            IPdfGeneratorService pdfGenerator,
            ILogger<FaturamentoEngine> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _pdfGenerator = pdfGenerator ?? throw new ArgumentNullException(nameof(pdfGenerator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<FaturamentoResult> FaturarOrdemAsync(int osId)
        {
            var os = await _context.OrdensServico
                .Include(o => o.Cliente)
                .FirstOrDefaultAsync(o => o.Id == osId);

            if (os == null)
            {
                return FaturamentoResult.Falha($"Ordem de Serviço #{osId} não encontrada.");
            }

            // Idempotência: se já existe fatura pendente, reutiliza para evitar duplicidade
            var faturaPendente = await _context.Faturamentos
                .FirstOrDefaultAsync(f => f.OrdemServicoId == osId && f.StatusPagamento == PagamentoStatus.Pendente);

            if (faturaPendente != null)
            {
                _logger.LogInformation("Fatura pendente existente #{FaturamentoId} reutilizada para a OS #{OsId}.", faturaPendente.Id, osId);
                return FaturamentoResult.Ok(faturaPendente, "Fatura pendente já existente para esta Ordem de Serviço.");
            }

            decimal total = (os.CustoPecas + os.CustoMaoDeObra) - os.DescontoAplicado;
            if (total <= 0)
            {
                return FaturamentoResult.Falha("O valor total da Ordem de Serviço deve ser maior que zero para faturamento.");
            }

            // Cálculo dos tributos
            decimal baseIss = os.CustoMaoDeObra;
            decimal valorIss = Math.Round(baseIss * AliquotaIssPadrao, 2);

            decimal baseIcms = os.CustoPecas;
            decimal valorIcms = Math.Round(baseIcms * AliquotaIcmsPadrao, 2);

            // Geração de BR Code Pix Dinâmico
            string txId = Guid.NewGuid().ToString("N").Substring(0, 25);
            string valorFormatado = total.ToString("0.00", CultureInfo.InvariantCulture);
            string qrcodeBase = $"00020101021226580014br.gov.bcb.pix0136{Guid.NewGuid()}5204000053039865405{valorFormatado}5802BR5915AssistenciaTech6009Sao Paulo62290525{txId}6304ABCD";

            var faturamento = new Faturamento
            {
                OrdemServicoId = osId,
                ValorTotal = total,
                DataVencimento = DateTime.UtcNow.AddDays(3),
                StatusPagamento = PagamentoStatus.Pendente,
                TxIdPix = txId,
                QrCodePayload = qrcodeBase,

                BaseCalculoISS = baseIss,
                AliquotaISS = AliquotaIssPadrao,
                ValorISS = valorIss,

                BaseCalculoICMS = baseIcms,
                AliquotaICMS = AliquotaIcmsPadrao,
                ValorICMS = valorIcms
            };

            _context.Faturamentos.Add(faturamento);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Fatura #{FaturamentoId} emitida com sucesso para OS #{OsId} no valor de R$ {ValorTotal:F2}.", faturamento.Id, osId, total);
            return FaturamentoResult.Ok(faturamento, "Fatura emitida com sucesso.");
        }

        public async Task<WebhookReconciliationResult> ProcessarWebhookPixAsync(string payload, string? signatureHeader)
        {
            var webhookSecret = _configuration["WebhookSecret"];
            if (string.IsNullOrEmpty(webhookSecret) || webhookSecret.Length < MinimoCaracteresSecret)
            {
                _logger.LogError("WebhookSecret não configurado ou com tamanho inferior a 32 caracteres.");
                return WebhookReconciliationResult.Falha(500, "Internal server error: WebhookSecret is missing or too short.");
            }

            if (string.IsNullOrWhiteSpace(signatureHeader))
            {
                _logger.LogWarning("Assinatura do webhook ausente no cabeçalho X-Webhook-Signature.");
                return WebhookReconciliationResult.Falha(401, "Invalid or missing webhook signature.");
            }

            if (!VerificarAssinatura(webhookSecret, signatureHeader, payload))
            {
                _logger.LogWarning("Assinatura do webhook inválida.");
                return WebhookReconciliationResult.Falha(401, "Invalid or missing webhook signature.");
            }

            List<string> txIds;
            try
            {
                txIds = ExtrairTxIdsFromJson(payload);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Payload JSON de webhook inválido.");
                return WebhookReconciliationResult.Falha(400, "Invalid JSON payload.");
            }

            if (txIds.Count == 0)
            {
                return WebhookReconciliationResult.Ok(txIds, 0);
            }

            int liquidadas = await ReconciliarPagamentosAsync(txIds);
            _logger.LogInformation("Webhook processado com sucesso: {QuantidadeTx} TxIds reconciliados, {TotalLiquidadas} faturas liquidadas.", txIds.Count, liquidadas);
            return WebhookReconciliationResult.Ok(txIds, liquidadas);
        }

        public async Task<FaturamentoResult> LiquidarFaturaManualmenteAsync(int faturamentoId)
        {
            var faturamento = await _context.Faturamentos.FindAsync(faturamentoId);
            if (faturamento == null)
            {
                return FaturamentoResult.Falha($"Faturamento #{faturamentoId} não encontrado.");
            }

            if (faturamento.StatusPagamento == PagamentoStatus.Pago_Total)
            {
                return FaturamentoResult.Ok(faturamento, "Faturamento já se encontrava liquidado.");
            }

            faturamento.StatusPagamento = PagamentoStatus.Pago_Total;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Faturamento #{FaturamentoId} liquidado manualmente com sucesso.", faturamentoId);
            return FaturamentoResult.Ok(faturamento, "Faturamento liquidado com sucesso.");
        }

        public async Task<ArtefatoFiscalResult> GerarXmlNfseAsync(int faturamentoId)
        {
            var faturamento = await _context.Faturamentos
                .AsNoTracking()
                .Include(f => f.OrdemServico)
                    .ThenInclude(os => os!.Cliente)
                .FirstOrDefaultAsync(f => f.Id == faturamentoId);

            if (faturamento == null)
            {
                return ArtefatoFiscalResult.Falha("Faturamento não encontrado.", 404);
            }

            if (faturamento.OrdemServico == null || faturamento.OrdemServico.Cliente == null)
            {
                return ArtefatoFiscalResult.Falha("Dados da OS ou Cliente estão incompletos.", 400);
            }

            var os = faturamento.OrdemServico;
            var cliente = os.Cliente;

            XNamespace ns = "http://www.abrasf.org.br/nfse.xsd";
            var xml = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(ns + "GerarNfseEnvio",
                    new XElement(ns + "Rps",
                        new XElement(ns + "InfDeclaracaoPrestacaoServico",
                            new XElement(ns + "Competencia", DateTime.UtcNow.ToString("yyyy-MM-dd")),
                            new XElement(ns + "Servico",
                                new XElement(ns + "Valores",
                                    new XElement(ns + "ValorServicos", os.CustoMaoDeObra.ToString("0.00", CultureInfo.InvariantCulture)),
                                    new XElement(ns + "ValorDeducoes", "0.00"),
                                    new XElement(ns + "ValorPis", "0.00"),
                                    new XElement(ns + "ValorCofins", "0.00"),
                                    new XElement(ns + "ValorInss", "0.00"),
                                    new XElement(ns + "ValorIr", "0.00"),
                                    new XElement(ns + "ValorCsll", "0.00"),
                                    new XElement(ns + "IssRetido", "2"),
                                    new XElement(ns + "ValorIss", faturamento.ValorISS.ToString("0.00", CultureInfo.InvariantCulture)),
                                    new XElement(ns + "BaseCalculo", faturamento.BaseCalculoISS.ToString("0.00", CultureInfo.InvariantCulture)),
                                    new XElement(ns + "Aliquota", (faturamento.AliquotaISS * 100).ToString("0.00", CultureInfo.InvariantCulture))
                                ),
                                new XElement(ns + "Discriminacao", $"Serviço referente à OS #{os.Id} - Equipamento: {os.Equipamento}")
                            ),
                            new XElement(ns + "Prestador",
                                new XElement(ns + "Cnpj", "12345678000199"),
                                new XElement(ns + "InscricaoMunicipal", "123456")
                            ),
                            new XElement(ns + "Tomador",
                                new XElement(ns + "IdentificacaoTomador",
                                    new XElement(ns + "CpfCnpj",
                                        new XElement(ns + "Cpf", cliente.Cpf?.Replace(".", "").Replace("-", "") ?? "00000000000")
                                    )
                                ),
                                new XElement(ns + "RazaoSocial", cliente.Nome),
                                new XElement(ns + "Contato",
                                    new XElement(ns + "Telefone", cliente.Telefone),
                                    new XElement(ns + "Email", cliente.Email)
                                )
                            )
                        )
                    )
                )
            );

            using var memoryStream = new MemoryStream();
            using var xmlWriter = System.Xml.XmlWriter.Create(memoryStream, new System.Xml.XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                OmitXmlDeclaration = false
            });

            xml.Save(xmlWriter);
            xmlWriter.Flush();

            return ArtefatoFiscalResult.Ok(memoryStream.ToArray(), $"Nfse_Fatura_{faturamentoId}.xml", "application/xml");
        }

        public async Task<ArtefatoFiscalResult> GerarReciboPagamentoPdfAsync(int faturamentoId)
        {
            var faturamento = await _context.Faturamentos
                .AsNoTracking()
                .Include(f => f.OrdemServico)
                    .ThenInclude(os => os!.Cliente)
                .FirstOrDefaultAsync(f => f.Id == faturamentoId);

            if (faturamento == null)
            {
                return ArtefatoFiscalResult.Falha("Faturamento não encontrado.", 404);
            }

            if (faturamento.StatusPagamento != PagamentoStatus.Pago_Total)
            {
                return ArtefatoFiscalResult.Falha("Este faturamento ainda não foi pago.", 400);
            }

            if (faturamento.OrdemServico == null || faturamento.OrdemServico.Cliente == null)
            {
                return ArtefatoFiscalResult.Falha("Dados da OS ou Cliente incompletos.", 400);
            }

            var pdf = _pdfGenerator.GenerateReciboPagamentoPdf(faturamento);
            return ArtefatoFiscalResult.Ok(pdf, $"Recibo_Fatura_{faturamentoId}.pdf", "application/pdf");
        }

        private bool VerificarAssinatura(string webhookSecret, string providedSignature, string payload)
        {
            byte[] secretBytes = Encoding.UTF8.GetBytes(webhookSecret);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            using var hmac = new HMACSHA256(secretBytes);
            byte[] computedHashBytes = hmac.ComputeHash(payloadBytes);
            byte[] providedSignatureBytes;
            try
            {
                providedSignatureBytes = Convert.FromHexString(providedSignature);
            }
            catch (FormatException)
            {
                return false;
            }

            if (providedSignatureBytes.Length != computedHashBytes.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(providedSignatureBytes, computedHashBytes);
        }

        private List<string> ExtrairTxIdsFromJson(string payload)
        {
            using var jsonDoc = JsonDocument.Parse(payload);

            if (jsonDoc.RootElement.TryGetProperty("pix", out var pixElement) && pixElement.ValueKind == JsonValueKind.Array)
            {
                var txIds = new List<string>(pixElement.GetArrayLength());
                foreach (var pix in pixElement.EnumerateArray())
                {
                    if (pix.TryGetProperty("txid", out var txidElement))
                    {
                        string? txId = txidElement.GetString();
                        if (!string.IsNullOrEmpty(txId))
                        {
                            txIds.Add(txId);
                        }
                    }
                }
                return txIds;
            }

            if (jsonDoc.RootElement.TryGetProperty("txid", out var singleTxidElement))
            {
                string? txId = singleTxidElement.GetString();
                if (!string.IsNullOrEmpty(txId))
                {
                    return new List<string>(1) { txId };
                }
            }

            return new List<string>(0);
        }

        private async Task<int> ReconciliarPagamentosAsync(List<string> txIds)
        {
            if (txIds == null || txIds.Count == 0) return 0;

            if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                var faturamentosToUpdate = await _context.Faturamentos
                    .Where(f => txIds.Contains(f.TxIdPix) && f.StatusPagamento != PagamentoStatus.Pago_Total)
                    .ToListAsync();

                if (faturamentosToUpdate.Count > 0)
                {
                    foreach (var faturamento in faturamentosToUpdate)
                    {
                        faturamento.StatusPagamento = PagamentoStatus.Pago_Total;
                    }
                    await _context.SaveChangesAsync();
                }
                return faturamentosToUpdate.Count;
            }

            return await _context.Faturamentos
                .Where(f => txIds.Contains(f.TxIdPix) && f.StatusPagamento != PagamentoStatus.Pago_Total)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.StatusPagamento, PagamentoStatus.Pago_Total));
        }
    }
}
