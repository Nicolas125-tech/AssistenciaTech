import re

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# We need a new TestDbContext that throws with inner exception, or we can just reuse TestExceptionDbContext if we change it or make another one.
# Actually, TestExceptionDbContext throws TestDbException. We can create TestInnerExceptionDbContext
new_context_class = """
        private class TestInnerExceptionDbContext : AppDbContext
        {
            public TestInnerExceptionDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                var inner = new Exception("Inner exception message");
                throw new Exception("Outer exception message", inner);
            }
        }
"""

content = content.replace("private class TestExceptionDbContext : AppDbContext", new_context_class + "\n        private class TestExceptionDbContext : AppDbContext")


new_test = """
        [Fact]
        public async Task Create_Post_ReturnsViewResult_WhenDatabaseFails_WithInnerException()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "CreatePostDbErrorInnerTest")
                .Options;

            using (var seedContext = new AppDbContext(options))
            {
                var cliente = new Cliente { Id = 1, Nome = "Cliente Teste", Cpf = "12345678901", Telefone = "123456789" };
                seedContext.Clientes.Add(cliente);
                await seedContext.SaveChangesAsync();
            }

            var exceptionContext = new TestInnerExceptionDbContext(options);

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
            var tempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(httpContext, Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
            localController.TempData = tempData;

            var os = new OrdemServicoCreateDto { Equipamento = "PC Teste", ClienteId = 1 };

            // Act
            var result = await localController.Create(new OrdemServicoCreateDto { ClienteId = os.ClienteId, Equipamento = os.Equipamento, ProblemaRelatado = os.ProblemaRelatado });

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Which;
            viewResult.ViewData.ModelState.ErrorCount.Should().BeGreaterThan(0);
            localController.ModelState.Values.SelectMany(v => v.Errors).Any(e => e.ErrorMessage.Contains("Outer exception message | Inner: Inner exception message")).Should().BeTrue();

            localController.Dispose();
            exceptionContext.Dispose();
        }
"""

# Insert the new test after Create_Post_ReturnsViewResult_WhenDatabaseFails
# find where Create_Post_ReturnsViewResult_WhenDatabaseFails ends
insert_pos = content.find("        [Fact]", content.find("public void Create_Get_ReturnsRedirectToActionResult_WhenDatabaseFails"))
content = content[:insert_pos] + new_test + "\n" + content[insert_pos:]

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
