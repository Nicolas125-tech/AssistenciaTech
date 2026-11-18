using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class TelegramWebhookControllerTests
    {
        private readonly Mock<ILogger<TelegramWebhookController>> _mockLogger;
        private readonly Mock<IMensageriaEngine> _mockMensageria;
        private readonly TelegramWebhookController _controller;

        public TelegramWebhookControllerTests()
        {
            _mockLogger = new Mock<ILogger<TelegramWebhookController>>();
            _mockMensageria = new Mock<IMensageriaEngine>();
            _controller = new TelegramWebhookController(_mockLogger.Object, _mockMensageria.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        private static JsonElement CreateUpdateJson(string text = "/start 1", long chatId = 123456)
        {
            using var doc = JsonDocument.Parse(
                JsonSerializer.Serialize(new { message = new { text, chat = new { id = chatId } } }));
            return doc.RootElement.Clone();
        }

        [Fact]
        public async Task Webhook_QuandoEngineRetornaNaoAutorizado_RetornaUnauthorized()
        {
            var json = CreateUpdateJson();
            _mockMensageria
                .Setup(m => m.ProcessarWebhookAsync(It.IsAny<JsonElement>(), "token-errado", It.IsAny<CancellationToken>()))
                .ReturnsAsync(WebhookProcessResult.NaoAutorizado());

            var result = await _controller.Webhook(json, "token-errado");

            result.Should().BeOfType<UnauthorizedResult>();
        }

        [Fact]
        public async Task Webhook_QuandoEngineProcessaComSucesso_RetornaOk()
        {
            var json = CreateUpdateJson("/start 15", 999);
            _mockMensageria
                .Setup(m => m.ProcessarWebhookAsync(It.IsAny<JsonElement>(), "secret-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(WebhookProcessResult.Processado(MensageriaDeliveryResult.Ok()));

            var result = await _controller.Webhook(json, "secret-123");

            result.Should().BeOfType<OkResult>();
            _mockMensageria.Verify(
                m => m.ProcessarWebhookAsync(It.IsAny<JsonElement>(), "secret-123", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Webhook_QuandoEngineIgnoraUpdate_RetornaOk()
        {
            var json = CreateUpdateJson();
            _mockMensageria
                .Setup(m => m.ProcessarWebhookAsync(It.IsAny<JsonElement>(), "secret-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(WebhookProcessResult.Ignorado());

            var result = await _controller.Webhook(json, "secret-123");

            result.Should().BeOfType<OkResult>();
        }

        [Fact]
        public async Task Webhook_QuandoEngineLancaExcecao_RegistraLogERetornaOk()
        {
            var json = CreateUpdateJson();
            _mockMensageria
                .Setup(m => m.ProcessarWebhookAsync(It.IsAny<JsonElement>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database error"));

            var result = await _controller.Webhook(json, "secret-123");

            result.Should().BeOfType<OkResult>();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
