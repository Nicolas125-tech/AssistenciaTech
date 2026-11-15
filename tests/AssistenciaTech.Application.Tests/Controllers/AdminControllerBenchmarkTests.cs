using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class AdminControllerBenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        public AdminControllerBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task MeasureEditGetPerformance()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AppDbContext(options);

            for (int i = 0; i < 1000; i++)
            {
                context.EquipamentosBackup.Add(new EquipamentoBackup { Descricao = $"Equip {i}", Disponivel = true });
            }
            context.OrdensServico.Add(new OrdemServico { Id = 1, ClienteId = 1, Equipamento = "PC", Status = "Aberto" });
            context.Clientes.Add(new Cliente { Id = 1, Nome = "Cliente 1" });
            await context.SaveChangesAsync();

            var estoqueMock = new Mock<IEstoqueEngine>();
            var notificationMock = new Mock<INotificationService>();
            var evidenciaStorageMock = new Mock<IEvidenciaStorage>();
            var lifecycle = new OrdemServicoLifecycle(
                context,
                estoqueMock.Object,
                notificationMock.Object,
                evidenciaStorageMock.Object,
                new Mock<ILogger<OrdemServicoLifecycle>>().Object);

            var mockPdfGeneratorService = new Mock<IPdfGeneratorService>();
            var mockDashboardService = new Mock<IAdminDashboardService>();

            var controller = new AdminController(
                new Mock<ILogger<AdminController>>().Object,
                lifecycle,
                mockDashboardService.Object,
                mockPdfGeneratorService.Object,
                evidenciaStorageMock.Object);

            // Warmup
            await controller.Edit(1);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                await controller.Edit(1);
            }
            sw.Stop();

            _output.WriteLine($"[EditGet] Time taken for 100 executions: {sw.ElapsedMilliseconds} ms");
        }
    }
}
