import re

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# Add _mockLogger.Verify to Index_Get_ReturnsEmptyList_WhenDatabaseFails
index_test_end = content.find('viewData["FaturamentoPrevisto"].Should().Be(0m);')
if index_test_end != -1:
    index_test_end = content.find('\n', index_test_end) + 1
    verify_logger = """
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("DB_CONNECTION_ERROR (Admin/Index)")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
"""
    content = content[:index_test_end] + verify_logger + content[index_test_end:]

# Add tests for Create GET and POST error paths
create_get_test = """
        [Fact]
        public void Create_Get_ReturnsRedirectToIndex_WhenDatabaseFails()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();

            var mockConnection = new Mock<System.Data.Common.DbConnection>();
            mockConnection.Setup(c => c.Open()).Throws(new Exception("Simulated DB connection error"));
            mockConnection.Setup(c => c.ConnectionString).Returns("DataSource=:memory:");
            mockConnection.Setup(c => c.State).Returns(System.Data.ConnectionState.Closed);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(mockConnection.Object)
                .Options;

            var exceptionContext = new AppDbContext(options);

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
            exceptionContext.Dispose();
        }

        [Fact]
        public async Task Create_Post_ReturnsRedirectToIndex_WhenDatabaseFails()
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
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Which;
            redirectResult.ActionName.Should().Be("Index");
            localController.TempData["ErroBanco"].Should().Be("Erro ao salvar ordem de serviço. O banco de dados está inacessível.");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("DB_CONNECTION_ERROR_CREATE_POST")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);

            localController.Dispose();
            exceptionContext.Dispose();
        }
"""

create_get_pos = content.find("public void Create_Get_ReturnsViewResult()")
if create_get_pos != -1:
    create_get_pos = content.rfind("[Fact]", 0, create_get_pos)
    content = content[:create_get_pos] + create_get_test + "\n" + content[create_get_pos:]

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
