using System;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssistenciaTech.Application.Tests
{
    public class EstoqueServiceTests
    {
        private AppDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public async Task DeduzirEstoque_ComPecasSuficientes_DeveDeduzirEstoqueERetornarTrue()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var peca = new Peca { Id = 1, Nome = "Peca 1", QuantidadeEstoque = 10, ValorUnitario = 100 };
            var ordemServico = new OrdemServico { Id = 1, ClienteId = 1, DataEntrada = DateTime.UtcNow };
            var ordemServicoPeca = new OrdemServicoPeca { Id = 1, OrdemServicoId = 1, PecaId = 1, Peca = peca, Quantidade = 5, ValorVenda = 100 };

            context.Pecas.Add(peca);
            context.OrdensServico.Add(ordemServico);
            context.OrdemServicoPecas.Add(ordemServicoPeca);
            await context.SaveChangesAsync();

            var service = new EstoqueService(context, Moq.Mock.Of<AssistenciaTech.Extensions.IResilientCacheService>());

            // Act
            var result = await service.DeduzirEstoque(1);

            // Assert
            result.Should().BeTrue();

            using var assertContext = GetDbContext(dbName);
            var pecaAtualizada = await assertContext.Pecas.FindAsync(1);
            pecaAtualizada.Should().NotBeNull();
            pecaAtualizada!.QuantidadeEstoque.Should().Be(5);
        }

        [Fact]
        public async Task DeduzirEstoque_ComEstoqueInsuficiente_DeveLancarInvalidOperationException()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var peca = new Peca { Id = 1, Nome = "Peca 1", QuantidadeEstoque = 2, ValorUnitario = 100 };
            var ordemServico = new OrdemServico { Id = 1, ClienteId = 1, DataEntrada = DateTime.UtcNow };
            var ordemServicoPeca = new OrdemServicoPeca { Id = 1, OrdemServicoId = 1, PecaId = 1, Peca = peca, Quantidade = 5, ValorVenda = 100 };

            context.Pecas.Add(peca);
            context.OrdensServico.Add(ordemServico);
            context.OrdemServicoPecas.Add(ordemServicoPeca);
            await context.SaveChangesAsync();

            var service = new EstoqueService(context, Moq.Mock.Of<AssistenciaTech.Extensions.IResilientCacheService>());

            // Act
            Func<Task> action = async () => await service.DeduzirEstoque(1);

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Estoque insuficiente para a peça: Peca 1. Disponível: 2, Solicitado: 5");
        }

        [Fact]
        public async Task DeduzirEstoque_SemPecas_DeveRetornarTrueENaoAlterarNada()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var service = new EstoqueService(context, Moq.Mock.Of<AssistenciaTech.Extensions.IResilientCacheService>());

            // Act
            var result = await service.DeduzirEstoque(999);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task RestaurarEstoque_ComPecas_DeveAumentarEstoqueERetornarTrue()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var peca = new Peca { Id = 1, Nome = "Peca 1", QuantidadeEstoque = 10, ValorUnitario = 100 };
            var ordemServico = new OrdemServico { Id = 1, ClienteId = 1, DataEntrada = DateTime.UtcNow };
            var ordemServicoPeca = new OrdemServicoPeca { Id = 1, OrdemServicoId = 1, PecaId = 1, Peca = peca, Quantidade = 5, ValorVenda = 100 };

            context.Pecas.Add(peca);
            context.OrdensServico.Add(ordemServico);
            context.OrdemServicoPecas.Add(ordemServicoPeca);
            await context.SaveChangesAsync();

            var service = new EstoqueService(context, Moq.Mock.Of<AssistenciaTech.Extensions.IResilientCacheService>());

            // Act
            var result = await service.RestaurarEstoque(1);

            // Assert
            result.Should().BeTrue();

            using var assertContext = GetDbContext(dbName);
            var pecaAtualizada = await assertContext.Pecas.FindAsync(1);
            pecaAtualizada.Should().NotBeNull();
            pecaAtualizada!.QuantidadeEstoque.Should().Be(15);
        }

        [Fact]
        public async Task RestaurarEstoque_SemPecas_DeveRetornarTrueENaoAlterarNada()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var service = new EstoqueService(context, Moq.Mock.Of<AssistenciaTech.Extensions.IResilientCacheService>());

            // Act
            var result = await service.RestaurarEstoque(999);

            // Assert
            result.Should().BeTrue();
        }
    }
}
