using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace AssistenciaTech.Infrastructure.Tests.Services
{
    public class TelegramNotificationServiceTests
    {
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<TelegramNotificationService>> _loggerMock;
        private readonly TelegramNotificationService _sut;

        public TelegramNotificationServiceTests()
        {
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(_httpMessageHandlerMock.Object);

            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<TelegramNotificationService>>();

            _sut = new TelegramNotificationService(httpClient, _configurationMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task EnviarNotificacaoStatusAsync_ShouldNotSend_WhenClienteIsNull()
        {
            // Arrange
            var os = new OrdemServico { Id = 1 };

            // Act
            await _sut.EnviarNotificacaoStatusAsync(null!, os, "Anterior");

            // Assert
            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task EnviarNotificacaoStatusAsync_ShouldNotSend_WhenTokenIsNotConfigured()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Test", TelegramChatId = "12345" };
            var os = new OrdemServico { Id = 1 };
            _configurationMock.Setup(c => c["TelegramBotToken"]).Returns((string)null!);

            // Act
            await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task EnviarNotificacaoStatusAsync_ShouldNotSend_WhenClienteTelegramChatIdIsEmpty()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Test", TelegramChatId = "" };
            var os = new OrdemServico { Id = 1 };
            _configurationMock.Setup(c => c["TelegramBotToken"]).Returns("valid_token");

            // Act
            await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task EnviarNotificacaoStatusAsync_ShouldSendNotification_WhenValidDataProvided()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Test", TelegramChatId = "12345" };
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.Concluido, ValorOrcamento = 100 };
            _configurationMock.Setup(c => c["TelegramBotToken"]).Returns("valid_token");

            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK
                });

            // Act
            await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri != null &&
                    req.RequestUri.ToString() == "https://api.telegram.org/botvalid_token/sendMessage"),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task EnviarNotificacaoStatusAsync_ShouldHandleApiError_WithoutThrowing()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Test", TelegramChatId = "12345" };
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.Concluido, ValorOrcamento = 100 };
            _configurationMock.Setup(c => c["TelegramBotToken"]).Returns("valid_token");

            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("Bad Request")
                });

            // Act
            var act = async () => await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task EnviarNotificacaoStatusAsync_ShouldHandleException_WithoutThrowing()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Test", TelegramChatId = "12345" };
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.Concluido, ValorOrcamento = 100 };
            _configurationMock.Setup(c => c["TelegramBotToken"]).Returns("valid_token");

            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var act = async () => await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            await act.Should().NotThrowAsync();
        }
    }
}
