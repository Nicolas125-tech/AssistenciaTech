using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
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

            // Seed data
            for (int i = 0; i < 1000; i++)
            {
                context.EquipamentosBackup.Add(new EquipamentoBackup { Descricao = $"Equip {i}", Disponivel = true });
            }
            context.OrdensServico.Add(new OrdemServico { Id = 1, ClienteId = 1, Equipamento = "PC", Status = "Aberto" });
            context.Clientes.Add(new Cliente { Id = 1, Nome = "Cliente 1" });
            await context.SaveChangesAsync();

            var mockEstoqueService = new Mock<IEstoqueService>();
            var mockEnv = new Mock<IWebHostEnvironment>();
            var mockPdfGeneratorService = new Mock<IPdfGeneratorService>();
            var mockDashboardService = new Mock<IAdminDashboardService>();
            var mockEquipamentoBackupService = new Mock<IEquipamentoBackupService>();
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockNotificationService = new Mock<INotificationService>();
            var mockClienteService = new Mock<IClienteService>();


            var cacheMock = new Mock<AssistenciaTech.Extensions.IResilientCacheService>();
            var cachedData = context.EquipamentosBackup.ToList();
            cacheMock.Setup(c => c.GetAsync<System.Collections.Generic.List<EquipamentoBackup>>(It.IsAny<string>()))
                .ReturnsAsync(cachedData);

            var mockFacade = new AdminFacade(
                mockEstoqueService.Object,
                mockEnv.Object,
                mockPdfGeneratorService.Object,
                mockDashboardService.Object,
                mockEquipamentoBackupService.Object,
                mockScopeFactory.Object,
                mockNotificationService.Object,
                mockClienteService.Object,
                cacheMock.Object
            );


            // This relies on the current constructor
            var controller = new AdminController(context, new Mock<ILogger<AdminController>>().Object, mockFacade);

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
