import re

with open("tests/AssistenciaTech.Application.Tests/Controllers/TelegramWebhookControllerTests.cs", "r") as f:
    content = f.read()

# Replace usings
content = content.replace("using AssistenciaTech.Controllers;", "using AssistenciaTech.Controllers;\nusing AssistenciaTech.Services.TelegramCommands;")

# Replace fields
content = re.sub(
    r"private readonly AppDbContext _context;.*?private readonly TelegramWebhookController _controller;",
    r"private readonly Mock<ILogger<TelegramWebhookController>> _mockLogger;\n        private readonly Mock<ITelegramCommandHandler> _mockCommandHandler;\n        private readonly TelegramWebhookController _controller;",
    content,
    flags=re.DOTALL
)

# Replace constructor
content = re.sub(
    r"public TelegramWebhookControllerTests\(\)\s*\{.*?_controller = new TelegramWebhookController\(_context, _mockLogger\.Object, _httpClient, _mockConfiguration\.Object\);\s*\}",
    r"""public TelegramWebhookControllerTests()
        {
            _mockLogger = new Mock<ILogger<TelegramWebhookController>>();
            _mockCommandHandler = new Mock<ITelegramCommandHandler>();
            _controller = new TelegramWebhookController(_mockLogger.Object, _mockCommandHandler.Object);
        }""",
    content,
    flags=re.DOTALL
)

# Replace Dispose method and IDisposable interface
content = re.sub(r" : IDisposable", "", content)
content = re.sub(r"public void Dispose\(\)\s*\{.*?\}", "", content, flags=re.DOTALL)

# Now, we need to rewrite all the tests.
tests = r"""
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

# Find where the first [Fact] starts and replace everything after it with our new tests
first_fact_index = content.find("[Fact]")
if first_fact_index != -1:
    content = content[:first_fact_index] + tests + "    }\n}\n"

with open("tests/AssistenciaTech.Application.Tests/Controllers/TelegramWebhookControllerTests.cs", "w") as f:
    f.write(content)
