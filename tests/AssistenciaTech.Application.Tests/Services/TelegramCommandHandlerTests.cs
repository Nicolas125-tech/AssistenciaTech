using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services.TelegramCommands;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class TelegramCommandHandlerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<ILogger<TelegramCommandHandler>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly TelegramCommandHandler _handler;

        public TelegramCommandHandlerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);

            _mockLogger = new Mock<ILogger<TelegramCommandHandler>>();

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

            _handler = new TelegramCommandHandler(_context, _mockLogger.Object, _httpClient, _mockConfiguration.Object);
        }

        public void Dispose()
        {
            try { _context.Database.EnsureDeleted(); } catch { }
            try { _context.Dispose(); } catch { }
            _httpClient.Dispose();
        }

        [Fact]
        public async Task HandleCommandAsync_ValidStartWithClienteId_UpdatesChatIdAndSendsMessage()
        {
            // Arrange
            var cliente = new Cliente { Nome = "Test Cliente", Email = "test@example.com" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var chatId = "987654321";
            var text = $"/start {cliente.Id}";

            // Act
            await _handler.HandleCommandAsync(text, chatId);

            // Assert
            var updatedCliente = await _context.Clientes.FindAsync(cliente.Id);
            updatedCliente.Should().NotBeNull();
            updatedCliente!.TelegramChatId.Should().Be(chatId);

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
        public async Task HandleCommandAsync_InvalidClienteId_SendsNotFoundMessage()
        {
            // Arrange
            var invalidId = 999;
            var chatId = "12345";
            var text = $"/start {invalidId}";

            // Act
            await _handler.HandleCommandAsync(text, chatId);

            // Assert
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
        public async Task HandleCommandAsync_OnlyStart_SendsInstructionMessage()
        {
            // Arrange
            var chatId = "12345";
            var text = "/start";

            // Act
            await _handler.HandleCommandAsync(text, chatId);

            // Assert
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
        public async Task HandleCommandAsync_NoTelegramToken_DoesNotSendHttpClient()
        {
            // Arrange
            var mockConfigEmpty = new Mock<IConfiguration>();
            mockConfigEmpty.Setup(c => c["TelegramBotToken"]).Returns(string.Empty);

            var handlerEmptyToken = new TelegramCommandHandler(_context, _mockLogger.Object, _httpClient, mockConfigEmpty.Object);

            var cliente = new Cliente { Nome = "Test Cliente", Email = "test@example.com" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var chatId = "987654321";
            var text = $"/start {cliente.Id}";

            // Act
            await handlerEmptyToken.HandleCommandAsync(text, chatId);

            // Assert
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
