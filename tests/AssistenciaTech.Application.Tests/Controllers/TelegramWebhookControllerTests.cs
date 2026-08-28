using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class TelegramWebhookControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<ILogger<TelegramWebhookController>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly TelegramWebhookController _controller;

        public TelegramWebhookControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);

            _mockLogger = new Mock<ILogger<TelegramWebhookController>>();

            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{}")
                });

            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

            _mockConfiguration = new Mock<IConfiguration>();
            _mockConfiguration.Setup(c => c["TelegramBotToken"]).Returns("dummy-token");

            _controller = new TelegramWebhookController(_context, _mockLogger.Object, _httpClient, _mockConfiguration.Object);
        }

        public void Dispose()
        {
            try { _context.Database.EnsureDeleted(); } catch { }
            try { _context.Dispose(); } catch { }
            _httpClient.Dispose();
        }

        private JsonElement CreateUpdateJson(string text, long chatId = 123456)
        {
            var json = $@"
            {{
                ""message"": {{
                    ""text"": ""{text}"",
                    ""chat"": {{
                        ""id"": {chatId}
                    }}
                }}
            }}";

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        private JsonElement CreateEmptyUpdateJson()
        {
            var json = "{}";
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        [Fact]
        public async Task Webhook_ValidStartWithClienteId_UpdatesChatIdAndSendsMessage()
        {
            // Arrange
            var cliente = new Cliente { Nome = "Test Cliente", Email = "test@example.com" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var chatId = 987654321;
            var json = CreateUpdateJson($"/start {cliente.Id}", chatId);

            // Act
            var result = await _controller.Webhook(json);

            // Assert
            result.Should().BeOfType<OkResult>();

            var updatedCliente = await _context.Clientes.FindAsync(cliente.Id);
            updatedCliente.Should().NotBeNull();
            updatedCliente!.TelegramChatId.Should().Be(chatId.ToString());

            // Verify HttpClient was called to send message
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("dummy-token")),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task Webhook_InvalidClienteId_SendsNotFoundMessage()
        {
            // Arrange
            var invalidId = 999;
            var chatId = 12345;
            var json = CreateUpdateJson($"/start {invalidId}", chatId);

            // Act
            var result = await _controller.Webhook(json);

            // Assert
            result.Should().BeOfType<OkResult>();

            // Verify HttpClient was called to send message
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("dummy-token")),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task Webhook_OnlyStart_SendsInstructionMessage()
        {
            // Arrange
            var chatId = 12345;
            var json = CreateUpdateJson("/start", chatId);

            // Act
            var result = await _controller.Webhook(json);

            // Assert
            result.Should().BeOfType<OkResult>();

            // Verify HttpClient was called to send message
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("dummy-token")),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task Webhook_InvalidJson_ReturnsOk()
        {
            // Arrange
            var json = CreateEmptyUpdateJson();

            // Act
            var result = await _controller.Webhook(json);

            // Assert
            result.Should().BeOfType<OkResult>();

            // Verify HttpClient was not called
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task Webhook_Exception_ReturnsOkAndLogsError()
        {
            // Arrange
            // We simulate an exception by disposing the context before calling the method,
            // so trying to access _context.Clientes will throw ObjectDisposedException
            _context.Dispose();

            var json = CreateUpdateJson("/start 1");

            // Act
            var result = await _controller.Webhook(json);

            // Assert
            result.Should().BeOfType<OkResult>();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once
            );
        }

        [Fact]
        public async Task Webhook_NoTelegramToken_ReturnsOkAndDoesNotSendHttpClient()
        {
            // Arrange
            var _mockConfigEmpty = new Mock<IConfiguration>();
            _mockConfigEmpty.Setup(c => c["TelegramBotToken"]).Returns(string.Empty);

            var _controllerEmptyToken = new TelegramWebhookController(_context, _mockLogger.Object, _httpClient, _mockConfigEmpty.Object);

            var cliente = new Cliente { Nome = "Test Cliente", Email = "test@example.com" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var chatId = 987654321;
            var json = CreateUpdateJson($"/start {cliente.Id}", chatId);

            // Act
            var result = await _controllerEmptyToken.Webhook(json);

            // Assert
            result.Should().BeOfType<OkResult>();

            // Verify HttpClient was NOT called to send message because token is empty
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }
    }
}
