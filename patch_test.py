with open("tests/AssistenciaTech.Application.Tests/Controllers/TelegramWebhookControllerTests.cs", "r") as f:
    content = f.read()

import re

# Fix using
if "using AssistenciaTech.Services.TelegramCommands;" not in content:
    content = content.replace("using AssistenciaTech.Controllers;", "using AssistenciaTech.Controllers;\nusing AssistenciaTech.Services.TelegramCommands;")

# Fix constructor signature
content = re.sub(
    r"private readonly AppDbContext _context;.*?private readonly TelegramWebhookController _controller;",
    r"private readonly Mock<ILogger<TelegramWebhookController>> _mockLogger;\n        private readonly Mock<ITelegramCommandHandler> _mockCommandHandler;\n        private readonly TelegramWebhookController _controller;",
    content,
    flags=re.DOTALL
)

# Fix setup
content = re.sub(
    r"public TelegramWebhookControllerTests\(\)\s*\{.*?\}",
    r"""public TelegramWebhookControllerTests()
        {
            _mockLogger = new Mock<ILogger<TelegramWebhookController>>();
            _mockCommandHandler = new Mock<ITelegramCommandHandler>();
            _controller = new TelegramWebhookController(_mockLogger.Object, _mockCommandHandler.Object);
        }""",
    content,
    flags=re.DOTALL
)

# Remove Dispose
content = re.sub(r"public void Dispose\(\)\s*\{.*?\}", "", content, flags=re.DOTALL)
content = content.replace(" : IDisposable", "")

# Rewrite tests
tests_replacement = r"""
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
"""

content = re.sub(r"\[Fact\].*", tests_replacement + "    }\n}", content, flags=re.DOTALL)

with open("tests/AssistenciaTech.Application.Tests/Controllers/TelegramWebhookControllerTests.cs", "w") as f:
    f.write(content)
