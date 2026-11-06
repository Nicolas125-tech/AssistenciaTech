using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class FaturamentoEngineTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<IPdfGeneratorService> _mockPdfGenerator;
        private readonly Mock<ILogger<FaturamentoEngine>> _mockLogger;
        private readonly FaturamentoEngine _engine;
        private const string ValidSecret = "chave_secreta_webhook_pix_com_mais_de_32_chars_12345";

        public FaturamentoEngineTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("DataSource=:memory:;Foreign Keys=False")
                .Options;

            _context = new AppDbContext(options);
            _context.Database.OpenConnection();
            _context.Database.EnsureCreated();

            _mockConfig = new Mock<IConfiguration>();
            _mockConfig.Setup(c => c["WebhookSecret"]).Returns(ValidSecret);

            _mockPdfGenerator = new Mock<IPdfGeneratorService>();
            _mockLogger = new Mock<ILogger<FaturamentoEngine>>();

            _engine = new FaturamentoEngine(
                _context,
                _mockConfig.Object,
                _mockPdfGenerator.Object,
                _mockLogger.Object
            );
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            GC.SuppressFinalize(this);
        }

        private string GerarAssinaturaHmac(string secret, string payload)
        {
            byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            using var hmac = new HMACSHA256(secretBytes);
            byte[] hash = hmac.ComputeHash(payloadBytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        [Fact]
        public async Task FaturarOrdemAsync_OrdemInexistente_RetornaFalha()
        {
            var result = await _engine.FaturarOrdemAsync(9999);

            result.Sucesso.Should().BeFalse();
            result.Mensagem.Should().Contain("não encontrada");
        }

        [Fact]
        public async Task FaturarOrdemAsync_ValorTotalMenorOuIgualZero_RetornaFalha()
        {
            var cliente = new Cliente { Nome = "Cliente Teste", Cpf = "12345678901", Email = "teste@cliente.com" };
            var os = new OrdemServico
            {
                Equipamento = "Notebook",
                ProblemaRelatado = "Aquecimento",
                Cliente = cliente,
                CustoPecas = 0,
                CustoMaoDeObra = 0,
                DescontoAplicado = 0
            };
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var result = await _engine.FaturarOrdemAsync(os.Id);

            result.Sucesso.Should().BeFalse();
            result.Mensagem.Should().Contain("maior que zero");
        }

        [Fact]
        public async Task FaturarOrdemAsync_ValoresValidos_CalculaTributosECriaFatura()
        {
            var cliente = new Cliente { Nome = "Cliente Teste", Cpf = "12345678901", Email = "teste@cliente.com" };
            var os = new OrdemServico
            {
                Equipamento = "Notebook Gamer",
                ProblemaRelatado = "Curto na placa",
                Cliente = cliente,
                CustoPecas = 200m,
                CustoMaoDeObra = 300m,
                DescontoAplicado = 50m
            };
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var result = await _engine.FaturarOrdemAsync(os.Id);

            result.Sucesso.Should().BeTrue();
            result.Faturamento.Should().NotBeNull();
            var fatura = result.Faturamento!;

            fatura.ValorTotal.Should().Be(450m); // 200 + 300 - 50
            fatura.StatusPagamento.Should().Be(PagamentoStatus.Pendente);
            fatura.BaseCalculoISS.Should().Be(300m);
            fatura.ValorISS.Should().Be(15m); // 5% de 300
            fatura.BaseCalculoICMS.Should().Be(200m);
            fatura.ValorICMS.Should().Be(36m); // 18% de 200
            fatura.TxIdPix.Should().NotBeNullOrWhiteSpace();
            fatura.QrCodePayload.Should().Contain("br.gov.bcb.pix");
            fatura.QrCodePayload.Should().Contain(fatura.TxIdPix);
        }

        [Fact]
        public async Task FaturarOrdemAsync_FaturaPendenteJaExistente_GaranteIdempotenciaEReutiliza()
        {
            var cliente = new Cliente { Nome = "Cliente Teste", Cpf = "12345678901" };
            var os = new OrdemServico
            {
                Equipamento = "PC Oficina",
                ProblemaRelatado = "Fonte queimada",
                Cliente = cliente,
                CustoPecas = 100m,
                CustoMaoDeObra = 100m
            };
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var primeiraTentativa = await _engine.FaturarOrdemAsync(os.Id);
            primeiraTentativa.Sucesso.Should().BeTrue();

            var segundaTentativa = await _engine.FaturarOrdemAsync(os.Id);
            segundaTentativa.Sucesso.Should().BeTrue();
            segundaTentativa.Faturamento!.Id.Should().Be(primeiraTentativa.Faturamento!.Id);
            segundaTentativa.Mensagem.Should().Contain("já existente");

            var totalFaturas = await _context.Faturamentos.CountAsync(f => f.OrdemServicoId == os.Id);
            totalFaturas.Should().Be(1);
        }

        [Fact]
        public async Task ProcessarWebhookPixAsync_SecretCurtoOuAusente_RetornaStatus500()
        {
            _mockConfig.Setup(c => c["WebhookSecret"]).Returns("curto");

            var result = await _engine.ProcessarWebhookPixAsync("{}", "assinatura");

            result.Sucesso.Should().BeFalse();
            result.HttpStatusCode.Should().Be(500);
            result.MensagemErro.Should().Contain("WebhookSecret is missing or too short");
        }

        [Fact]
        public async Task ProcessarWebhookPixAsync_AssinaturaAusente_RetornaStatus401()
        {
            var result = await _engine.ProcessarWebhookPixAsync("{}", null);

            result.Sucesso.Should().BeFalse();
            result.HttpStatusCode.Should().Be(401);
            result.MensagemErro.Should().Contain("Invalid or missing webhook signature");
        }

        [Fact]
        public async Task ProcessarWebhookPixAsync_AssinaturaInvalida_RetornaStatus401()
        {
            var payload = "{\"teste\": 123}";
            var result = await _engine.ProcessarWebhookPixAsync(payload, "deadbeef123456");

            result.Sucesso.Should().BeFalse();
            result.HttpStatusCode.Should().Be(401);
            result.MensagemErro.Should().Contain("Invalid or missing webhook signature");
        }

        [Fact]
        public async Task ProcessarWebhookPixAsync_PayloadJsonInvalido_RetornaStatus400()
        {
            var payload = "invalido { json";
            var assinatura = GerarAssinaturaHmac(ValidSecret, payload);

            var result = await _engine.ProcessarWebhookPixAsync(payload, assinatura);

            result.Sucesso.Should().BeFalse();
            result.HttpStatusCode.Should().Be(400);
            result.MensagemErro.Should().Contain("Invalid JSON payload");
        }

        [Fact]
        public async Task ProcessarWebhookPixAsync_PayloadComArrayPixEAssinaturaValida_ReconciliaPagamentos()
        {
            var fatura1 = new Faturamento
            {
                OrdemServicoId = 1,
                ValorTotal = 250m,
                DataVencimento = DateTime.UtcNow.AddDays(1),
                StatusPagamento = PagamentoStatus.Pendente,
                TxIdPix = "txid_pix_reconciliar_01"
            };
            var fatura2 = new Faturamento
            {
                OrdemServicoId = 2,
                ValorTotal = 400m,
                DataVencimento = DateTime.UtcNow.AddDays(1),
                StatusPagamento = PagamentoStatus.Pendente,
                TxIdPix = "txid_pix_reconciliar_02"
            };
            _context.Faturamentos.AddRange(fatura1, fatura2);
            await _context.SaveChangesAsync();

            var payload = "{\"pix\": [{\"txid\": \"txid_pix_reconciliar_01\"}, {\"txid\": \"txid_pix_reconciliar_02\"}]}";
            var assinatura = GerarAssinaturaHmac(ValidSecret, payload);

            var result = await _engine.ProcessarWebhookPixAsync(payload, assinatura);

            result.Sucesso.Should().BeTrue();
            result.HttpStatusCode.Should().Be(200);
            result.TxIdsReconciliados.Should().HaveCount(2);
            result.TotalFaturasLiquidadas.Should().Be(2);

            _context.ChangeTracker.Clear();
            var atualizada1 = await _context.Faturamentos.FindAsync(fatura1.Id);
            var atualizada2 = await _context.Faturamentos.FindAsync(fatura2.Id);
            atualizada1!.StatusPagamento.Should().Be(PagamentoStatus.Pago_Total);
            atualizada2!.StatusPagamento.Should().Be(PagamentoStatus.Pago_Total);
        }

        [Fact]
        public async Task ProcessarWebhookPixAsync_PayloadComTxIdRaiz_ReconciliaPagamento()
        {
            var fatura = new Faturamento
            {
                OrdemServicoId = 3,
                ValorTotal = 180m,
                DataVencimento = DateTime.UtcNow.AddDays(1),
                StatusPagamento = PagamentoStatus.Pendente,
                TxIdPix = "txid_raiz_01"
            };
            _context.Faturamentos.Add(fatura);
            await _context.SaveChangesAsync();

            var payload = "{\"txid\": \"txid_raiz_01\"}";
            var assinatura = GerarAssinaturaHmac(ValidSecret, payload);

            var result = await _engine.ProcessarWebhookPixAsync(payload, assinatura);

            result.Sucesso.Should().BeTrue();
            result.TotalFaturasLiquidadas.Should().Be(1);

            _context.ChangeTracker.Clear();
            var atualizada = await _context.Faturamentos.FindAsync(fatura.Id);
            atualizada!.StatusPagamento.Should().Be(PagamentoStatus.Pago_Total);
        }

        [Fact]
        public async Task LiquidarFaturaManualmenteAsync_FaturamentoInexistente_RetornaFalha()
        {
            var result = await _engine.LiquidarFaturaManualmenteAsync(9999);

            result.Sucesso.Should().BeFalse();
            result.Mensagem.Should().Contain("não encontrado");
        }

        [Fact]
        public async Task LiquidarFaturaManualmenteAsync_FaturamentoPendente_AtualizaParaPagoTotal()
        {
            var fatura = new Faturamento
            {
                OrdemServicoId = 4,
                ValorTotal = 320m,
                DataVencimento = DateTime.UtcNow.AddDays(2),
                StatusPagamento = PagamentoStatus.Pendente
            };
            _context.Faturamentos.Add(fatura);
            await _context.SaveChangesAsync();

            var result = await _engine.LiquidarFaturaManualmenteAsync(fatura.Id);

            result.Sucesso.Should().BeTrue();
            result.Faturamento!.StatusPagamento.Should().Be(PagamentoStatus.Pago_Total);

            _context.ChangeTracker.Clear();
            var persistida = await _context.Faturamentos.FindAsync(fatura.Id);
            persistida!.StatusPagamento.Should().Be(PagamentoStatus.Pago_Total);
        }

        [Fact]
        public async Task FaturarOrdemAsync_ValoresFracionados_ArredondaParaDuasCasasDecimais()
        {
            var cliente = new Cliente { Nome = "Cliente Teste", Cpf = "12345678901", Email = "teste@cliente.com" };
            var os = new OrdemServico
            {
                Equipamento = "Notebook",
                ProblemaRelatado = "Troca de pasta",
                Cliente = cliente,
                CustoMaoDeObra = 100.33m, // 100.33 * 0.05 = 5.0165 -> 5.02
                CustoPecas = 100.55m // 100.55 * 0.18 = 18.099 -> 18.10
            };
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var result = await _engine.FaturarOrdemAsync(os.Id);

            result.Sucesso.Should().BeTrue();
            result.Faturamento!.ValorISS.Should().Be(5.02m);
            result.Faturamento!.ValorICMS.Should().Be(18.10m);
        }

        [Fact]
        public async Task GerarXmlNfseAsync_ComClienteSemCpf_SubstituiPorZeros()
        {
            var cliente = new Cliente { Nome = "Empresa Sem CPF", Cpf = "00000000000" };
            var os = new OrdemServico { Equipamento = "Servidor", ProblemaRelatado = "Fonte", Cliente = cliente, CustoMaoDeObra = 200m };
            var fatura = new Faturamento
            {
                OrdemServico = os,
                ValorTotal = 200m,
                DataVencimento = DateTime.UtcNow
            };
            _context.Faturamentos.Add(fatura);
            await _context.SaveChangesAsync();

            var result = await _engine.GerarXmlNfseAsync(fatura.Id);

            result.Sucesso.Should().BeTrue();
            var xmlString = Encoding.UTF8.GetString(result.ConteudoBytes!);
            xmlString.Should().Contain("00000000000");
        }

        [Fact]
        public async Task GerarXmlNfseAsync_FaturamentoInexistente_RetornaStatus404()
        {
            var result = await _engine.GerarXmlNfseAsync(8888);

            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task GerarXmlNfseAsync_ClienteNull_RetornaStatus404()
        {
            var os = new OrdemServico { Equipamento = "Sem Cliente", ProblemaRelatado = "Teste" };
            var fatura = new Faturamento
            {
                OrdemServico = os,
                ValorTotal = 100m,
                DataVencimento = DateTime.UtcNow
            };
            _context.Faturamentos.Add(fatura);
            await _context.SaveChangesAsync();

            var result = await _engine.GerarXmlNfseAsync(fatura.Id);

            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task GerarXmlNfseAsync_FaturamentoValido_RetornaXmlAbrasf()
        {
            var cliente = new Cliente { Nome = "Maria Santos", Cpf = "987.654.321-00", Email = "maria@email.com", Telefone = "11988887777" };
            var os = new OrdemServico { Equipamento = "Dell Inspiron", ProblemaRelatado = "Bateria", Cliente = cliente, CustoMaoDeObra = 150m, CustoPecas = 100m };
            var fatura = new Faturamento
            {
                OrdemServico = os,
                ValorTotal = 250m,
                DataVencimento = DateTime.UtcNow,
                BaseCalculoISS = 150m,
                AliquotaISS = 0.05m,
                ValorISS = 7.50m
            };
            _context.Faturamentos.Add(fatura);
            await _context.SaveChangesAsync();

            var result = await _engine.GerarXmlNfseAsync(fatura.Id);

            result.Sucesso.Should().BeTrue();
            result.ContentType.Should().Be("application/xml");
            result.NomeArquivo.Should().Be($"Nfse_Fatura_{fatura.Id}.xml");
            result.ConteudoBytes.Should().NotBeNullOrEmpty();

            var xmlString = Encoding.UTF8.GetString(result.ConteudoBytes!);
            xmlString.Should().Contain("GerarNfseEnvio");
            xmlString.Should().Contain("98765432100");
            xmlString.Should().Contain("Maria Santos");
            xmlString.Should().Contain("Dell Inspiron");
        }

        [Fact]
        public async Task GerarReciboPagamentoPdfAsync_NaoPago_RetornaStatus400()
        {
            var cliente = new Cliente { Nome = "Carlos", Cpf = "11122233344" };
            var os = new OrdemServico { Equipamento = "MacBook", ProblemaRelatado = "Teclado", Cliente = cliente };
            var fatura = new Faturamento
            {
                OrdemServico = os,
                ValorTotal = 500m,
                DataVencimento = DateTime.UtcNow,
                StatusPagamento = PagamentoStatus.Pendente
            };
            _context.Faturamentos.Add(fatura);
            await _context.SaveChangesAsync();

            var result = await _engine.GerarReciboPagamentoPdfAsync(fatura.Id);

            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(400);
            result.MensagemErro.Should().Contain("ainda não foi pago");
        }

        [Fact]
        public async Task GerarReciboPagamentoPdfAsync_FaturamentoPago_RetornaPdf()
        {
            var cliente = new Cliente { Nome = "Carlos", Cpf = "11122233344" };
            var os = new OrdemServico { Equipamento = "MacBook", ProblemaRelatado = "Teclado", Cliente = cliente };
            var fatura = new Faturamento
            {
                OrdemServico = os,
                ValorTotal = 500m,
                DataVencimento = DateTime.UtcNow,
                StatusPagamento = PagamentoStatus.Pago_Total
            };
            _context.Faturamentos.Add(fatura);
            await _context.SaveChangesAsync();

            var fakePdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
            _mockPdfGenerator.Setup(p => p.GenerateReciboPagamentoPdf(It.IsAny<Faturamento>())).Returns(fakePdf);

            var result = await _engine.GerarReciboPagamentoPdfAsync(fatura.Id);

            result.Sucesso.Should().BeTrue();
            result.ContentType.Should().Be("application/pdf");
            result.NomeArquivo.Should().Be($"Recibo_Fatura_{fatura.Id}.pdf");
            result.ConteudoBytes.Should().Equal(fakePdf);
        }
    }
}
