using System;
using System.Collections.Generic;
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
    public class AdminDashboardServiceTests
    {
        private AppDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private async Task SeedDataAsync(AppDbContext context)
        {
            var cliente1 = new Cliente { Id = 1, Nome = "João Silva", Cpf = "11111111111", Telefone = "11999999999" };
            var cliente2 = new Cliente { Id = 2, Nome = "Maria Santos", Cpf = "22222222222", Telefone = "11888888888" };

            context.Clientes.AddRange(cliente1, cliente2);

            var ordens = new List<OrdemServico>
            {
                new OrdemServico
                {
                    Id = 1, ClienteId = 1, Equipamento = "Notebook Dell", ProblemaRelatado = "Não liga",
                    Status = WorkflowStatus.Recebido, ValorOrcamento = 100m, DataEntrada = DateTime.UtcNow.AddDays(-5)
                },
                new OrdemServico
                {
                    Id = 2, ClienteId = 2, Equipamento = "PC Gamer", ProblemaRelatado = "Lento",
                    Status = WorkflowStatus.EmAnalise, ValorOrcamento = 200m, DataEntrada = DateTime.UtcNow.AddDays(-4)
                },
                new OrdemServico
                {
                    Id = 3, ClienteId = 1, Equipamento = "MacBook Pro", ProblemaRelatado = "Teclado ruim",
                    Status = WorkflowStatus.Concluido, ValorOrcamento = 500m, DataEntrada = DateTime.UtcNow.AddDays(-3),
                    DataConclusao = DateTime.UtcNow.AddDays(-1)
                },
                new OrdemServico
                {
                    Id = 4, ClienteId = 2, Equipamento = "Monitor LG", ProblemaRelatado = "Sem imagem",
                    Status = WorkflowStatus.Entregue, ValorOrcamento = 300m, DataEntrada = DateTime.UtcNow.AddDays(-10),
                    DataConclusao = DateTime.UtcNow.AddDays(-5), DataEntregaCliente = DateTime.UtcNow.AddDays(-2)
                },
                new OrdemServico
                {
                    Id = 5, ClienteId = 1, Equipamento = "Impressora HP", ProblemaRelatado = "Não imprime",
                    Status = "Cancelado", ValorOrcamento = 150m, DataEntrada = DateTime.UtcNow.AddDays(-2)
                }
            };

            context.OrdensServico.AddRange(ordens);
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetDashboardDataAsync_WithoutFilters_ReturnsCorrectAggregations()
        {
            // Arrange
            using var context = GetDbContext();
            await SeedDataAsync(context);
            var service = new AdminDashboardService(context, Moq.Mock.Of<AssistenciaTech.Services.IEstoqueService>(), null);

            // Act
            var result = await service.GetDashboardDataAsync(string.Empty, string.Empty);

            // Assert
            result.Should().NotBeNull();
            result.Ordens.Should().HaveCount(5);

            // Expected ordens ordered by DataEntrada descending
            result.Ordens.First().Id.Should().Be(5); // Newest
            result.Ordens.Last().Id.Should().Be(4); // Oldest

            result.ChartLabels.Should().BeEquivalentTo(new[] { WorkflowStatus.Recebido, WorkflowStatus.EmAnalise, WorkflowStatus.Concluido, WorkflowStatus.Entregue, "Cancelado" });
            result.ChartData.Should().BeEquivalentTo(new[] { 1, 1, 1, 1, 1 });

            // TotalAbertas: status != Concluido && status != Entregue
            // Abertas: Recebido (1), EmAnalise (1), Cancelado (1) -> 3
            result.TotalAbertas.Should().Be(3);

            // EquipamentosProntos: status == Concluido
            // Prontos: Concluido (1) -> 1
            result.EquipamentosProntos.Should().Be(1);

            // FaturamentoPrevisto: status != Entregue && status != Cancelado
            // Recebido (100) + EmAnalise (200) + Concluido (500) -> 800
            result.FaturamentoPrevisto.Should().Be(800m);
        }

        [Fact]
        public async Task GetDashboardDataAsync_WithSearchString_ReturnsFilteredData()
        {
            // Arrange
            using var context = GetDbContext();
            await SeedDataAsync(context);
            var service = new AdminDashboardService(context, Moq.Mock.Of<AssistenciaTech.Services.IEstoqueService>(), null);

            // Act 1: Search by Client Name ("Maria")
            var result1 = await service.GetDashboardDataAsync("Maria", string.Empty);

            // Assert 1
            result1.Ordens.Should().HaveCount(2);
            result1.Ordens.All(o => o.Cliente.Nome == "Maria Santos").Should().BeTrue();
            result1.TotalAbertas.Should().Be(1); // EmAnalise
            result1.FaturamentoPrevisto.Should().Be(200m); // EmAnalise

            // Act 2: Search by Equipment ("MacBook")
            var result2 = await service.GetDashboardDataAsync("MacBook", string.Empty);

            // Assert 2
            result2.Ordens.Should().HaveCount(1);
            result2.Ordens.First().Equipamento.Should().Contain("MacBook");

            // Act 3: Search by Order ID ("4")
            var result3 = await service.GetDashboardDataAsync("4", string.Empty);

            // Assert 3
            result3.Ordens.Should().HaveCount(1);
            result3.Ordens.First().Id.Should().Be(4);
        }

        [Fact]
        public async Task GetDashboardDataAsync_WithStatusFilter_ReturnsFilteredData()
        {
            // Arrange
            using var context = GetDbContext();
            await SeedDataAsync(context);
            var service = new AdminDashboardService(context, Moq.Mock.Of<AssistenciaTech.Services.IEstoqueService>(), null);

            // Act
            var result = await service.GetDashboardDataAsync(string.Empty, WorkflowStatus.Concluido);

            // Assert
            result.Ordens.Should().HaveCount(1);
            result.Ordens.First().Status.Should().Be(WorkflowStatus.Concluido);

            result.TotalAbertas.Should().Be(0); // Only Concluido, so 0 Abertas
            result.EquipamentosProntos.Should().Be(1);
            result.FaturamentoPrevisto.Should().Be(500m);

            result.ChartLabels.Should().BeEquivalentTo(new[] { WorkflowStatus.Concluido });
            result.ChartData.Should().BeEquivalentTo(new[] { 1 });
        }
    }
}
