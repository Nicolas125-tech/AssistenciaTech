using System;
using System.Linq;
using System.Net;
using System.Net.Http;
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
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly HttpClient _httpClient;
        private readonly TelegramCommandHandler _sut;

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
            _mockConfiguration.Setup(c => c["TelegramBotToken"]).Returns("fake-token");

            _sut = new TelegramCommandHandler(_context, _mockLogger.Object, _httpClient, _mockConfiguration.Object);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task HandleCommandAsync_WithNullOrWhiteSpaceText_ReturnsEarly(string? text)
        {
            // Arrange
#pragma warning disable CS8604
            // Act
            await _sut.HandleCommandAsync(text, "chat-id");
#pragma warning restore CS8604

            // Assert
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task HandleCommandAsync_WithStartAndValidClienteId_UpdatesClienteAndSendsMessage()
        {
            // Arrange
            var cliente = new Cliente { Id = 15, Nome = "Test Client" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            _context.ChangeTracker.Clear();

            // Act
            await _sut.HandleCommandAsync("/start 15", "chat-id-123");

            // Assert
            var updatedCliente = await _context.Clientes.FindAsync(15);
            updatedCliente.Should().NotBeNull();
            updatedCliente!.TelegramChatId.Should().Be("chat-id-123");

            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == "https://api.telegram.org/botfake-token/sendMessage"),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task HandleCommandAsync_WithStartAndInvalidClienteId_SendsNotFoundMessage()
        {
            // Arrange - empty db

            // Act
            await _sut.HandleCommandAsync("/start 999", "chat-id-123");

            // Assert
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == "https://api.telegram.org/botfake-token/sendMessage"),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task HandleCommandAsync_WithStartAndNonNumericId_DoesNotUpdateOrSendMessage()
        {
            // Arrange
            var cliente = new Cliente { Id = 15, Nome = "Test Client" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            // Act
            await _sut.HandleCommandAsync("/start abc", "chat-id-123");

            // Assert
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task HandleCommandAsync_WithStartNoId_SendsHelpMessage()
        {
            // Arrange

            // Act
            await _sut.HandleCommandAsync("/start", "chat-id-123");

            // Assert
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == "https://api.telegram.org/botfake-token/sendMessage"),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task HandleCommandAsync_WithValidClienteIdButNoTelegramToken_UpdatesClienteButDoesNotSendMessage()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["TelegramBotToken"]).Returns((string?)null);

            var cliente = new Cliente { Id = 20, Nome = "Test Client 20" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            _context.ChangeTracker.Clear();

            // Act
            await _sut.HandleCommandAsync("/start 20", "chat-id-456");

            // Assert
            var updatedCliente = await _context.Clientes.FindAsync(20);
            updatedCliente.Should().NotBeNull();
            updatedCliente!.TelegramChatId.Should().Be("chat-id-456");

            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _httpClient.Dispose();
        }
    }
}
