using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class FaturamentosControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IFaturamentoEngine> _mockEngine;
        private readonly FaturamentosController _controller;

        public FaturamentosControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("DataSource=:memory:;Foreign Keys=False")
                .Options;

            _context = new AppDbContext(options);
            _context.Database.OpenConnection();
            _context.Database.EnsureCreated();

            _mockEngine = new Mock<IFaturamentoEngine>();

            _controller = new FaturamentosController(_context, _mockEngine.Object);

            var httpContext = new DefaultHttpContext();
            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            _controller.TempData = tempData;
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task Index_ReturnsViewResult_WithListOfFaturamentos()
        {
            var cliente = new Cliente { Nome = "Cliente Teste", Cpf = "12345678901", Email = "cliente@teste.com", Telefone = "11999999999" };
            var os = new OrdemServico { Equipamento = "Note", ProblemaRelatado = "Tela", Cliente = cliente, Status = "Recebido" };

            var faturamento1 = new Faturamento { OrdemServico = os, ValorTotal = 150, DataVencimento = DateTime.UtcNow };
            var faturamento2 = new Faturamento { OrdemServico = os, ValorTotal = 300, DataVencimento = DateTime.UtcNow };

            _context.Faturamentos.AddRange(faturamento1, faturamento2);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var result = await _controller.Index();

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<Faturamento>>().Subject;
            var list = model.ToList();

            list.Should().HaveCount(2);
            list.Should().Contain(f => f.Id == faturamento1.Id);
            list.Should().Contain(f => f.Id == faturamento2.Id);
        }

        [Fact]
        public async Task GerarDaOS_QuandoSucesso_RedirecionaParaIndex()
        {
            var faturamento = new Faturamento { Id = 10, ValorTotal = 250m };
            _mockEngine.Setup(e => e.FaturarOrdemAsync(123))
                .ReturnsAsync(FaturamentoResult.Ok(faturamento));

            var result = await _controller.GerarDaOS(123);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be(nameof(_controller.Index));
            _mockEngine.Verify(e => e.FaturarOrdemAsync(123), Times.Once);
        }

        [Fact]
        public async Task GerarDaOS_QuandoOsNaoEncontrada_RetornaNotFound()
        {
            _mockEngine.Setup(e => e.FaturarOrdemAsync(999))
                .ReturnsAsync(FaturamentoResult.Falha("Ordem de Serviço #999 não encontrada."));

            var result = await _controller.GerarDaOS(999);

            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task GerarDaOS_QuandoFalhaDeNegocio_DefineTempDataERedireciona()
        {
            _mockEngine.Setup(e => e.FaturarOrdemAsync(456))
                .ReturnsAsync(FaturamentoResult.Falha("O valor total da Ordem de Serviço deve ser maior que zero para faturamento."));

            var result = await _controller.GerarDaOS(456);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be(nameof(_controller.Index));
            _controller.TempData["MensagemErro"].Should().Be("O valor total da Ordem de Serviço deve ser maior que zero para faturamento.");
        }

        [Fact]
        public async Task WebhookPix_QuandoFalha500_RetornaStatusCode500()
        {
            _controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
            _mockEngine.Setup(e => e.ProcessarWebhookPixAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(WebhookReconciliationResult.Falha(500, "Internal server error: WebhookSecret is missing or too short."));

            var result = await _controller.WebhookPix();

            var objResult = result.Should().BeOfType<ObjectResult>().Subject;
            objResult.StatusCode.Should().Be(500);
            objResult.Value.Should().Be("Internal server error: WebhookSecret is missing or too short.");
        }

        [Fact]
        public async Task WebhookPix_QuandoFalha401_RetornaUnauthorized()
        {
            _controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
            _mockEngine.Setup(e => e.ProcessarWebhookPixAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(WebhookReconciliationResult.Falha(401, "Invalid or missing webhook signature."));

            var result = await _controller.WebhookPix();

            var unauthResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthResult.Value.Should().Be("Invalid or missing webhook signature.");
        }

        [Fact]
        public async Task WebhookPix_QuandoFalha400_RetornaBadRequest()
        {
            _controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{invalido"));
            _mockEngine.Setup(e => e.ProcessarWebhookPixAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(WebhookReconciliationResult.Falha(400, "Invalid JSON payload."));

            var result = await _controller.WebhookPix();

            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Invalid JSON payload.");
        }

        [Fact]
        public async Task WebhookPix_QuandoSucesso_RetornaOk()
        {
            _controller.Request.Headers["X-Webhook-Signature"] = "valid_sig";
            _controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"txid\": \"123\"}"));
            _mockEngine.Setup(e => e.ProcessarWebhookPixAsync("{\"txid\": \"123\"}", "valid_sig"))
                .ReturnsAsync(WebhookReconciliationResult.Ok(new List<string> { "123" }, 1));

            var result = await _controller.WebhookPix();

            result.Should().BeOfType<OkResult>();
        }

        [Fact]
        public async Task MarcarPago_QuandoSucesso_RedirecionaParaIndex()
        {
            var fatura = new Faturamento { Id = 5, StatusPagamento = PagamentoStatus.Pago_Total };
            _mockEngine.Setup(e => e.LiquidarFaturaManualmenteAsync(5))
                .ReturnsAsync(FaturamentoResult.Ok(fatura));

            var result = await _controller.MarcarPago(5);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be(nameof(_controller.Index));
        }

        [Fact]
        public async Task MarcarPago_QuandoFalha_DefineTempDataERedireciona()
        {
            _mockEngine.Setup(e => e.LiquidarFaturaManualmenteAsync(999))
                .ReturnsAsync(FaturamentoResult.Falha("Faturamento #999 não encontrado."));

            var result = await _controller.MarcarPago(999);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be(nameof(_controller.Index));
            _controller.TempData["MensagemErro"].Should().Be("Faturamento #999 não encontrado.");
        }

        [Fact]
        public async Task GerarXmlNfse_Quando404_RetornaNotFound()
        {
            _mockEngine.Setup(e => e.GerarXmlNfseAsync(999))
                .ReturnsAsync(ArtefatoFiscalResult.Falha("Faturamento não encontrado.", 404));

            var result = await _controller.GerarXmlNfse(999);

            var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFound.Value.Should().Be("Faturamento não encontrado.");
        }

        [Fact]
        public async Task GerarXmlNfse_Quando400_RetornaBadRequest()
        {
            _mockEngine.Setup(e => e.GerarXmlNfseAsync(5))
                .ReturnsAsync(ArtefatoFiscalResult.Falha("Dados da OS ou Cliente estão incompletos.", 400));

            var result = await _controller.GerarXmlNfse(5);

            var badReq = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badReq.Value.Should().Be("Dados da OS ou Cliente estão incompletos.");
        }

        [Fact]
        public async Task GerarXmlNfse_QuandoSucesso_RetornaFileResult()
        {
            var bytes = Encoding.UTF8.GetBytes("<xml>nfse</xml>");
            _mockEngine.Setup(e => e.GerarXmlNfseAsync(7))
                .ReturnsAsync(ArtefatoFiscalResult.Ok(bytes, "Nfse_Fatura_7.xml", "application/xml"));

            var result = await _controller.GerarXmlNfse(7);

            var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
            fileResult.ContentType.Should().Be("application/xml");
            fileResult.FileDownloadName.Should().Be("Nfse_Fatura_7.xml");
            fileResult.FileContents.Should().Equal(bytes);
        }

        [Fact]
        public async Task GerarReciboPagamento_Quando404_RetornaNotFound()
        {
            _mockEngine.Setup(e => e.GerarReciboPagamentoPdfAsync(999))
                .ReturnsAsync(ArtefatoFiscalResult.Falha("Faturamento não encontrado.", 404));

            var result = await _controller.GerarReciboPagamento(999);

            var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFound.Value.Should().Be("Faturamento não encontrado.");
        }

        [Fact]
        public async Task GerarReciboPagamento_Quando400_RetornaBadRequest()
        {
            _mockEngine.Setup(e => e.GerarReciboPagamentoPdfAsync(3))
                .ReturnsAsync(ArtefatoFiscalResult.Falha("Este faturamento ainda não foi pago.", 400));

            var result = await _controller.GerarReciboPagamento(3);

            var badReq = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badReq.Value.Should().Be("Este faturamento ainda não foi pago.");
        }

        [Fact]
        public async Task GerarReciboPagamento_QuandoSucesso_RetornaFileResult()
        {
            var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
            _mockEngine.Setup(e => e.GerarReciboPagamentoPdfAsync(8))
                .ReturnsAsync(ArtefatoFiscalResult.Ok(pdfBytes, "Recibo_Fatura_8.pdf", "application/pdf"));

            var result = await _controller.GerarReciboPagamento(8);

            var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
            fileResult.ContentType.Should().Be("application/pdf");
            fileResult.FileDownloadName.Should().Be("Recibo_Fatura_8.pdf");
            fileResult.FileContents.Should().Equal(pdfBytes);
        }
    }
}
