using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class AdminDashboardServiceBenchmarkTests
    {
        private AppDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private async Task SeedLargeDataAsync(AppDbContext context, int count)
        {
            var cliente = new Cliente { Id = 1, Nome = "João Benchmark", Cpf = "33333333333", Telefone = "11777777777" };
            context.Clientes.Add(cliente);

            var ordens = new List<OrdemServico>(count);
            for (int i = 1; i <= count; i++)
            {
                ordens.Add(new OrdemServico
                {
                    Id = i + 10,
                    ClienteId = 1,
                    Equipamento = $"Equipamento {i}",
                    ProblemaRelatado = "Problema teste",
                    Status = WorkflowStatus.Recebido,
                    ValorOrcamento = 100m,
                    DataEntrada = DateTime.UtcNow.AddMinutes(-i)
                });
            }

            context.OrdensServico.AddRange(ordens);
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetDashboardDataAsync_PerformanceBenchmark_LoadsQuicklyWithPagination()
        {
            // Arrange
            using var context = GetDbContext();
            int recordCount = 5000;
            await SeedLargeDataAsync(context, recordCount);
            var service = new AdminDashboardService(context);

            var sw = new Stopwatch();

            // Warmup (Ef Core Initialization overhead can skew first run)
            await service.GetDashboardDataAsync(string.Empty, string.Empty, 1, 10);

            // Act - Fetch first page of 50 items
            sw.Start();
            var result = await service.GetDashboardDataAsync(string.Empty, string.Empty, 1, 50);
            sw.Stop();

            // Assert
            result.Should().NotBeNull();
            result.TotalOrdens.Should().Be(recordCount);
            result.Ordens.Count.Should().Be(50); // Only 50 loaded in memory!

            // Assert performance - should be very fast since we only take 50 items
            sw.ElapsedMilliseconds.Should().BeLessThan(2000, "Querying large datasets should be fast with pagination");

            // Note: Without .Take(), all 5000 entities would be loaded into memory, taking significantly longer and using more RAM.
        }
    }
}
