import re

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

test_code = """
        [Fact]
        public async Task Edit_Post_RedirectsToIndex_AndLogsError_WhenNotificationFails()
        {
            // Arrange
            var cliente = new Cliente { Nome = "Cliente Teste", Email = "teste@teste.com", Telefone = "12345678", Cpf = "12345678901" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var osExistente = new OrdemServico { ClienteId = cliente.Id, Status = "Orçamento", Equipamento = "PC", Id = 101 };
            _context.OrdensServico.Add(osExistente);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var osAlterada = new OrdemServico
            {
                Id = 101,
                ClienteId = cliente.Id,
                Equipamento = "PC Atualizado",
                Status = "Aprovado" // Changed status to trigger notification
            };

            var mockNotificationService = new Mock<AssistenciaTech.Services.INotificationService>();
            mockNotificationService
                .Setup(n => n.EnviarNotificacaoStatusAsync(It.IsAny<Cliente>(), It.IsAny<OrdemServico>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Notification service down"));

            var mockEquipamentoBackupService = new Mock<IEquipamentoBackupService>();
            var localController = new AdminController(
                _context,
                _mockEstoqueService.Object,
                _mockEnv.Object,
                _mockPdfGeneratorService.Object,
                _mockDashboardService.Object,
                mockEquipamentoBackupService.Object,
                _mockLogger.Object,
                _mockScopeFactory.Object,
                mockNotificationService.Object
            );

            // Act
            var result = await localController.Edit(101, osAlterada, null);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Which;
            redirectResult.ActionName.Should().Be("Index");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Erro ao enviar notificação para OS #101")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);

            var osDb = await _context.OrdensServico.FindAsync(101);
            osDb.Status.Should().Be("Aprovado");

            localController.Dispose();
        }
"""

if "Edit_Post_RedirectsToIndex_AndLogsError_WhenNotificationFails" not in content:
    content = content.replace('public async Task Edit_Post_ReturnsView_WhenDatabaseThrowsException()', test_code + '\n        [Fact]\n        public async Task Edit_Post_ReturnsView_WhenDatabaseThrowsException()')
    with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
        f.write(content)
    print("Test injected")
else:
    print("Test already exists")
