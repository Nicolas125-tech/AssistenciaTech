import re

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

replacement = """
        [Fact]
        public void Create_Get_ReturnsRedirectToIndex_WhenDatabaseFails()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var mockContext = new Mock<AppDbContext>(options);
            // Simulate an exception when accessing the Clientes DbSet
            mockContext.Setup(c => c.Clientes).Throws(new Exception("Simulated DB connection error"));

            var localController = new AdminController(
                mockContext.Object,
                _mockEstoqueService.Object,
                _mockEnv.Object,
                _mockPdfGeneratorService.Object,
                _mockDashboardService.Object,
                new Mock<IEquipamentoBackupService>().Object,
                _mockLogger.Object,
                _mockScopeFactory.Object,
                new Mock<INotificationService>().Object
            );

            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            localController.TempData = tempData;

            // Act
            var result = localController.Create();

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Which;
            redirectResult.ActionName.Should().Be("Index");
            localController.TempData["ErroBanco"].Should().Be("Não foi possível carregar a tela de criação. O banco de dados está inacessível.");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("DB_CONNECTION_ERROR_CREATE")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);

            localController.Dispose();
        }
"""

pattern = r'\[Fact\]\s+public void Create_Get_ReturnsRedirectToIndex_WhenDatabaseFails\(\).*?exceptionContext\.Dispose\(\);\s+\}'
content = re.sub(pattern, replacement.strip(), content, flags=re.DOTALL)

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
