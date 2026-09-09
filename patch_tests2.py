import re

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# Replace the incorrectly asserted create_post test
post_test_pattern = r'\[Fact\]\s+public async Task Create_Post_ReturnsRedirectToIndex_WhenDatabaseFails\(\).*?exceptionContext\.Dispose\(\);\s+\}'
post_test_replacement = """
        [Fact]
        public async Task Create_Post_ReturnsViewResult_WithModelStateError_WhenDatabaseFails()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var exceptionContext = new TestExceptionDbContext(options);

            var localController = new AdminController(
                exceptionContext,
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

            var dto = new OrdemServicoCreateDto
            {
                ClienteId = 1,
                Equipamento = "PC Teste",
                ProblemaRelatado = "Falha"
            };

            // Act
            var result = await localController.Create(dto);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Which;
            localController.ModelState.ErrorCount.Should().BeGreaterThan(0);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Erro ao salvar a Ordem de Serviço")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);

            localController.Dispose();
            exceptionContext.Dispose();
        }
"""
content = re.sub(post_test_pattern, post_test_replacement.strip(), content, flags=re.DOTALL)

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
