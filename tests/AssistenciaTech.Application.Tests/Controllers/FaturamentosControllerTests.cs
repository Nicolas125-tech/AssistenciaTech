using System;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
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
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly FaturamentosController _controller;

        public FaturamentosControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _mockConfiguration = new Mock<IConfiguration>();
            _controller = new FaturamentosController(_context, _mockConfiguration.Object);
        }

        private void SetHttpContext(string? signatureHeaderValue = null, string payload = "{}")
        {
            var httpContext = new DefaultHttpContext();

            // Set body
            var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload));
            httpContext.Request.Body = stream;
            httpContext.Request.ContentLength = stream.Length;

            if (signatureHeaderValue != null)
            {
                httpContext.Request.Headers["X-Webhook-Signature"] = signatureHeaderValue;
            }

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }
        [Fact]
        public async Task Index_ReturnsViewResult_WithListOfFaturamentos()
        {
            // Arrange
            var os = new OrdemServico
            {
                ClienteId = 1,
                Equipamento = "PC Gamer",
                ProblemaRelatado = "Não liga",
                Status = "Concluído",
                CustoPecas = 100m,
                CustoMaoDeObra = 200m,
                DescontoAplicado = 50m
            };
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var faturamentosList = new System.Collections.Generic.List<Faturamento>
            {
                new Faturamento { OrdemServicoId = os.Id, ValorTotal = 100, StatusPagamento = PagamentoStatus.Pendente },
                new Faturamento { OrdemServicoId = os.Id, ValorTotal = 200, StatusPagamento = PagamentoStatus.Pago_Total }
            };

            _context.Faturamentos.AddRange(faturamentosList);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<Faturamento>>().Subject;
            model.Should().HaveCount(2);
        }



        [Fact]
        public async Task WebhookPix_Returns500_WhenWebhookSecretIsNotConfigured()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["WebhookSecret"]).Returns((string)null);
            SetHttpContext();

            // Act
            var result = await _controller.WebhookPix();

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(500);
            objectResult.Value.Should().Be("Internal server error.");
        }

        [Fact]
        public async Task WebhookPix_Returns401_WhenWebhookSignatureHeaderIsMissing()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["WebhookSecret"]).Returns("my-secret");
            SetHttpContext(); // Default context has no custom headers

            // Act
            var result = await _controller.WebhookPix();

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().Be("Invalid or missing webhook signature.");
        }

        [Fact]
        public async Task WebhookPix_Returns401_WhenWebhookSignatureIsInvalid()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["WebhookSecret"]).Returns("my-secret");
            SetHttpContext("invalid-secret");

            // Act
            var result = await _controller.WebhookPix();

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().Be("Invalid or missing webhook signature.");
        }

        [Fact]
        public async Task WebhookPix_ReturnsOk_WhenWebhookSignatureIsValid_AndUpdatesFaturamento()
        {
            // Arrange
            string txId = Guid.NewGuid().ToString("N").Substring(0, 25);

            var faturamento = new Faturamento
            {
                OrdemServicoId = 1,
                ValorTotal = 100m,
                DataVencimento = DateTime.UtcNow.AddDays(3),
                StatusPagamento = PagamentoStatus.Pendente,
                TxIdPix = txId
            };

            _context.Faturamentos.Add(faturamento);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            string secret = "my-secret";
            string payload = $"{{\"pix\":[{{\"txid\":\"{txId}\"}}]}}";
            _mockConfiguration.Setup(c => c["WebhookSecret"]).Returns(secret);

            // Generate valid signature
            byte[] secretBytes = System.Text.Encoding.UTF8.GetBytes(secret);
            byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
            using var hmac = new System.Security.Cryptography.HMACSHA256(secretBytes);
            byte[] hash = hmac.ComputeHash(payloadBytes);
            string signature = Convert.ToHexString(hash).ToLowerInvariant();

            SetHttpContext(signature, payload);

            // Act
            var result = await _controller.WebhookPix();

            // Assert
            result.Should().BeOfType<OkResult>();

            var updatedFaturamento = await _context.Faturamentos.FindAsync(faturamento.Id);
            updatedFaturamento.Should().NotBeNull();
            updatedFaturamento!.StatusPagamento.Should().Be(PagamentoStatus.Pago_Total);
        }

        [Fact]
        public async Task WebhookPix_ReturnsBadRequest_WhenJsonIsInvalid()
        {
            // Arrange
            string secret = "my-secret";
            string payload = "invalid-json";
            _mockConfiguration.Setup(c => c["WebhookSecret"]).Returns(secret);

            // Generate valid signature
            byte[] secretBytes = System.Text.Encoding.UTF8.GetBytes(secret);
            byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
            using var hmac = new System.Security.Cryptography.HMACSHA256(secretBytes);
            byte[] hash = hmac.ComputeHash(payloadBytes);
            string signature = Convert.ToHexString(hash).ToLowerInvariant();

            SetHttpContext(signature, payload);

            // Act
            var result = await _controller.WebhookPix();

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Invalid JSON payload.");
        }


        [Fact]
        public async Task MarcarPago_ReturnsRedirectToIndex_AndUpdatesStatus_WhenFaturamentoExists()
        {
            // Arrange
            var faturamento = new Faturamento
            {
                OrdemServicoId = 1,
                ValorTotal = 150m,
                DataVencimento = DateTime.UtcNow.AddDays(5),
                StatusPagamento = PagamentoStatus.Pendente
            };

            _context.Faturamentos.Add(faturamento);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.MarcarPago(faturamento.Id);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Which;
            redirectResult.ActionName.Should().Be(nameof(FaturamentosController.Index));

            var updatedFaturamento = await _context.Faturamentos.FindAsync(faturamento.Id);
            updatedFaturamento.Should().NotBeNull();
            updatedFaturamento!.StatusPagamento.Should().Be(PagamentoStatus.Pago_Total);
        }


        [Fact]
        public async Task MarcarPago_ReturnsRedirectToIndex_AndDoesNotUpdate_WhenFaturamentoDoesNotExist()
        {
            // Arrange
            int nonExistentId = 999;

            // Act
            var result = await _controller.MarcarPago(nonExistentId);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Which;
            redirectResult.ActionName.Should().Be(nameof(FaturamentosController.Index));

            // We know it didn't update because it doesn't exist, but checking context count confirms no accidental inserts
            var count = await _context.Faturamentos.CountAsync();
            count.Should().Be(0);
        }



        [Fact]
        public async Task MarcarPago_ThrowsTestDbException_WhenDatabaseFails()
        {
            // Arrange
            // We use the existing _context (InMemory) to seed the data,
            // but we need to mock AppDbContext to throw exception.
            // Since FindAsync accesses the DbSet, we can mock SaveChangesAsync instead.

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var mockContext = new MockAppDbContext(options);

            var faturamento = new Faturamento
            {
                OrdemServicoId = 1,
                ValorTotal = 150m,
                DataVencimento = DateTime.UtcNow.AddDays(5),
                StatusPagamento = PagamentoStatus.Pendente
            };

            // Seed using a real context to avoid DbSet setup issues
            using (var seedContext = new AppDbContext(options))
            {
                seedContext.Faturamentos.Add(faturamento);
                await seedContext.SaveChangesAsync();
            }

            var mockConfig = new Mock<IConfiguration>();
            var controller = new FaturamentosController(mockContext, mockConfig.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<TestDbException>(() => controller.MarcarPago(faturamento.Id));
            exception.Message.Should().Be("Database connection failed");
        }

        public class MockAppDbContext : AppDbContext
        {
            public MockAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

            public override Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default)
            {
                throw new TestDbException("Database connection failed");
            }
        }

        public class TestDbException : System.Data.Common.DbException
        {
            public TestDbException(string message) : base(message) { }
        }


        [Fact]
        public async Task GerarDaOS_ReturnsRedirectToIndex_AndCreatesFaturamento_WhenOsExists()
        {
            // Arrange
            var os = new OrdemServico
            {
                ClienteId = 1,
                Equipamento = "PC Gamer",
                ProblemaRelatado = "Não liga",
                Status = "Concluído",
                CustoPecas = 100m,
                CustoMaoDeObra = 200m,
                DescontoAplicado = 50m
            };

            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.GerarDaOS(os.Id);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Which;
            redirectResult.ActionName.Should().Be(nameof(FaturamentosController.Index));

            var faturamento = await _context.Faturamentos.FirstOrDefaultAsync(f => f.OrdemServicoId == os.Id);
            faturamento.Should().NotBeNull();

            // Expected total = (100 + 200) - 50 = 250
            faturamento!.ValorTotal.Should().Be(250m);
            faturamento.StatusPagamento.Should().Be(PagamentoStatus.Pendente);
            faturamento.TxIdPix.Should().NotBeNullOrEmpty();
            faturamento.QrCodePayload.Should().NotBeNullOrEmpty();
            faturamento.DataVencimento.Date.Should().Be(DateTime.UtcNow.AddDays(3).Date);
        }

        [Fact]
        public async Task GerarDaOS_ReturnsNotFound_WhenOsDoesNotExist()
        {
            // Arrange
            int nonExistentId = 999;

            // Act
            var result = await _controller.GerarDaOS(nonExistentId);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
