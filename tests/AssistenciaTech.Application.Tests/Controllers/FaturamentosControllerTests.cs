using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class FaturamentosControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<ITributacaoService> _mockTributacaoService;
        private readonly Mock<INfseXmlGeneratorService> _mockXmlGenerator;
        private readonly Mock<IPdfGeneratorService> _mockPdfGenerator;
        private readonly FaturamentosController _controller;

        public FaturamentosControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);

            _mockConfig = new Mock<IConfiguration>();
            _mockTributacaoService = new Mock<ITributacaoService>();
            _mockXmlGenerator = new Mock<INfseXmlGeneratorService>();
            _mockPdfGenerator = new Mock<IPdfGeneratorService>();

            _controller = new FaturamentosController(
                _context,
                _mockConfig.Object,
                _mockTributacaoService.Object,
                _mockXmlGenerator.Object,
                _mockPdfGenerator.Object
            );

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            GC.SuppressFinalize(this);
        }

        private string GenerateSignature(string secret, string payload)
        {
            byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            using var hmac = new HMACSHA256(secretBytes);
            byte[] computedHashBytes = hmac.ComputeHash(payloadBytes);
            return Convert.ToHexString(computedHashBytes).ToLowerInvariant();
        }
        [Fact]
        public async Task WebhookPix_MissingOrShortSecret_ReturnsStatusCode500()
        {
            // Arrange
            _mockConfig.Setup(c => c["WebhookSecret"]).Returns("short_secret");

            // Act
            var result = await _controller.WebhookPix();

            // Assert
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
            statusCodeResult.Value.Should().Be("Internal server error: WebhookSecret is missing or too short.");
        }

        [Fact]
        public async Task WebhookPix_MissingSignatureHeader_ReturnsUnauthorized()
        {
            // Arrange
            var validSecret = new string('a', 32);
            _mockConfig.Setup(c => c["WebhookSecret"]).Returns(validSecret);

            // Act
            var result = await _controller.WebhookPix();

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().Be("Invalid or missing webhook signature.");
        }

        [Fact]
        public async Task WebhookPix_InvalidSignature_ReturnsUnauthorized()
        {
            // Arrange
            var validSecret = new string('a', 32);
            _mockConfig.Setup(c => c["WebhookSecret"]).Returns(validSecret);

            _controller.Request.Headers["X-Webhook-Signature"] = "invalid_signature";

            var payload = "{\"test\": 123}";
            _controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            // Act
            var result = await _controller.WebhookPix();

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().Be("Invalid or missing webhook signature.");
        }

        [Fact]
        public async Task WebhookPix_InvalidJson_ReturnsBadRequest()
        {
            // Arrange
            var validSecret = new string('a', 32);
            _mockConfig.Setup(c => c["WebhookSecret"]).Returns(validSecret);

            var payload = "invalid json payload";
            var signature = GenerateSignature(validSecret, payload);

            _controller.Request.Headers["X-Webhook-Signature"] = signature;
            _controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            // Act
            var result = await _controller.WebhookPix();

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Invalid JSON payload.");
        }

        [Fact]
        public async Task WebhookPix_ValidSignatureAndPayload_ProcessesPaymentAndReturnsOk()
        {
            // Arrange
            var validSecret = new string('a', 32);
            _mockConfig.Setup(c => c["WebhookSecret"]).Returns(validSecret);

            string txId = "tx123456789";
            var faturamento = new Faturamento
            {
                OrdemServicoId = 1,
                ValorTotal = 100,
                DataVencimento = DateTime.UtcNow.AddDays(1),
                StatusPagamento = PagamentoStatus.Pendente,
                TxIdPix = txId,
                QrCodePayload = "qr"
            };
            _context.Faturamentos.Add(faturamento);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var payload = $"{{\"txid\": \"{txId}\"}}";
            var signature = GenerateSignature(validSecret, payload);

            _controller.Request.Headers["X-Webhook-Signature"] = signature;
            _controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            // Act
            var result = await _controller.WebhookPix();

            // Assert
            result.Should().BeOfType<OkResult>();

            var updatedFaturamento = await _context.Faturamentos.FindAsync(faturamento.Id);
            updatedFaturamento.Should().NotBeNull();
            updatedFaturamento!.StatusPagamento.Should().Be(PagamentoStatus.Pago_Total);
        }

        [Fact]
        public async Task WebhookPix_ValidSignatureAndPixArrayPayload_ProcessesPaymentAndReturnsOk()
        {
            // Arrange
            var validSecret = new string('a', 32);
            _mockConfig.Setup(c => c["WebhookSecret"]).Returns(validSecret);

            string txId1 = "tx111";
            string txId2 = "tx222";

            var faturamento1 = new Faturamento { OrdemServicoId = 1, ValorTotal = 100, DataVencimento = DateTime.UtcNow, StatusPagamento = PagamentoStatus.Pendente, TxIdPix = txId1, QrCodePayload = "qr" };
            var faturamento2 = new Faturamento { OrdemServicoId = 2, ValorTotal = 200, DataVencimento = DateTime.UtcNow, StatusPagamento = PagamentoStatus.Pendente, TxIdPix = txId2, QrCodePayload = "qr" };

            _context.Faturamentos.AddRange(faturamento1, faturamento2);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var payload = $"{{\"pix\": [ {{\"txid\": \"{txId1}\"}}, {{\"txid\": \"{txId2}\"}} ]}}";
            var signature = GenerateSignature(validSecret, payload);

            _controller.Request.Headers["X-Webhook-Signature"] = signature;
            _controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            // Act
            var result = await _controller.WebhookPix();

            // Assert
            result.Should().BeOfType<OkResult>();

            var updatedFaturamento1 = await _context.Faturamentos.FindAsync(faturamento1.Id);
            updatedFaturamento1.Should().NotBeNull();
            updatedFaturamento1!.StatusPagamento.Should().Be(PagamentoStatus.Pago_Total);

            var updatedFaturamento2 = await _context.Faturamentos.FindAsync(faturamento2.Id);
            updatedFaturamento2.Should().NotBeNull();
            updatedFaturamento2!.StatusPagamento.Should().Be(PagamentoStatus.Pago_Total);
        }

                [Fact]
        public async Task GerarReciboPagamento_ReturnsNotFound_WhenFaturamentoDoesNotExist()
        {
            // Act
            var result = await _controller.GerarReciboPagamento(999);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().Be("Faturamento não encontrado.");
        }

        [Fact]
        public async Task GerarReciboPagamento_ReturnsBadRequest_WhenStatusIsNotPagoTotal()
        {
            // Arrange
            var faturamento = new Faturamento
            {
                Id = 1,
                OrdemServicoId = 1,
                ValorTotal = 100,
                DataVencimento = DateTime.Now,
                StatusPagamento = PagamentoStatus.Pendente, // Not Pago_Total
                OrdemServico = new OrdemServico { Id = 1, Cliente = new Cliente { Id = 1 } }
            };
            _context.Faturamentos.Add(faturamento);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.GerarReciboPagamento(1);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Este faturamento ainda não foi pago.");
        }

        [Fact]
        public async Task GerarReciboPagamento_ReturnsFile_WhenValid()
        {
            // Arrange
            var faturamento = new Faturamento
            {
                Id = 3,
                OrdemServicoId = 3,
                ValorTotal = 100,
                DataVencimento = DateTime.Now,
                StatusPagamento = PagamentoStatus.Pago_Total,
                OrdemServico = new OrdemServico { Id = 3, Cliente = new Cliente { Id = 3 } }
            };
            _context.Faturamentos.Add(faturamento);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var expectedPdfBytes = new byte[] { 1, 2, 3 };
            _mockPdfGenerator.Setup(p => p.GenerateReciboPagamentoPdf(It.IsAny<Faturamento>()))
                             .Returns(expectedPdfBytes);

            // Act
            var result = await _controller.GerarReciboPagamento(3);

            // Assert
            var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
            fileResult.ContentType.Should().Be("application/pdf");
            fileResult.FileDownloadName.Should().Be("Recibo_Fatura_3.pdf");
            fileResult.FileContents.Should().BeEquivalentTo(expectedPdfBytes);
        }
    }
}
