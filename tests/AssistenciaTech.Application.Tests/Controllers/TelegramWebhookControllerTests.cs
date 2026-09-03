using System;
using System.Text.Json;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Services.TelegramCommands;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class TelegramWebhookControllerTests
    {
        private readonly Mock<ILogger<TelegramWebhookController>> _mockLogger;
        private readonly Mock<ITelegramCommandHandler> _mockCommandHandler;
        private readonly TelegramWebhookController _controller;

        public TelegramWebhookControllerTests()
        {
            _mockLogger = new Mock<ILogger<TelegramWebhookController>>();
            _mockCommandHandler = new Mock<ITelegramCommandHandler>();
            _controller = new TelegramWebhookController(_mockLogger.Object, _mockCommandHandler.Object);
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
        public async Task Webhook_ValidJson_CallsHandlerAndReturnsOk()
        {
            // Arrange
            var text = "/start 1";
            var chatId = 123456;
            var json = CreateUpdateJson(text, chatId);

            // Act
            var result = await _controller.Webhook(json);

            // Assert
            result.Should().BeOfType<OkResult>();
            _mockCommandHandler.Verify(h => h.HandleCommandAsync(text, chatId.ToString()), Times.Once);
        }

        [Fact]
        public async Task Webhook_InvalidJson_DoesNotCallHandlerAndReturnsOk()
        {
            // Arrange
            var json = CreateEmptyUpdateJson();

            // Act
            var result = await _controller.Webhook(json);

            // Assert
            result.Should().BeOfType<OkResult>();
            _mockCommandHandler.Verify(h => h.HandleCommandAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Webhook_Exception_ReturnsOkAndLogsError()
        {
            // Arrange
            var json = CreateUpdateJson("/start");
            _mockCommandHandler.Setup(h => h.HandleCommandAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Test error"));

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
    }
}
