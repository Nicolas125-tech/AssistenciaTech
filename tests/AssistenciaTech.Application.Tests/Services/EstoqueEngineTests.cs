using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.DTOs;
using AssistenciaTech.Extensions;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class EstoqueEngineTests
    {
        private AppDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public async Task DeduzirEstoqueAsync_ComPecasSuficientes_DeveDeduzirEstoqueERetornarSucessoEInvalidarCache()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var peca = new Peca { Id = 1, Nome = "Tela iPhone 13", QuantidadeEstoque = 10, QuantidadeMinima = 2, ValorUnitario = 350 };
            var ordem = new OrdemServico { Id = 100, ClienteId = 1, DataEntrada = DateTime.UtcNow };
            var osPeca = new OrdemServicoPeca { Id = 1, OrdemServicoId = 100, PecaId = 1, Peca = peca, Quantidade = 4, ValorVenda = 350 };

            context.Pecas.Add(peca);
            context.OrdensServico.Add(ordem);
            context.OrdemServicoPecas.Add(osPeca);
            await context.SaveChangesAsync();

            var cacheMock = new Mock<IResilientCacheService>();
            var loggerMock = new Mock<ILogger<EstoqueEngine>>();
            var engine = new EstoqueEngine(context, cacheMock.Object, loggerMock.Object);

            // Act
            var result = await engine.DeduzirEstoqueAsync(100);

            // Assert
            result.Sucesso.Should().BeTrue();
            using var assertContext = GetDbContext(dbName);
            var pecaAtualizada = await assertContext.Pecas.FindAsync(1);
            pecaAtualizada!.QuantidadeEstoque.Should().Be(6);

            cacheMock.Verify(c => c.RemoveAsync("Estoque_AlertasEstoque"), Times.Once);
        }

        [Fact]
        public async Task DeduzirEstoqueAsync_ComEstoqueInsuficiente_DeveRetornarFalhaENaoAlterarSaldo()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var peca = new Peca { Id = 2, Nome = "Bateria MacBook", QuantidadeEstoque = 1, QuantidadeMinima = 2, ValorUnitario = 500 };
            var ordem = new OrdemServico { Id = 101, ClienteId = 1, DataEntrada = DateTime.UtcNow };
            var osPeca = new OrdemServicoPeca { Id = 2, OrdemServicoId = 101, PecaId = 2, Peca = peca, Quantidade = 3, ValorVenda = 500 };

            context.Pecas.Add(peca);
            context.OrdensServico.Add(ordem);
            context.OrdemServicoPecas.Add(osPeca);
            await context.SaveChangesAsync();

            var cacheMock = new Mock<IResilientCacheService>();
            var loggerMock = new Mock<ILogger<EstoqueEngine>>();
            var engine = new EstoqueEngine(context, cacheMock.Object, loggerMock.Object);

            // Act
            var result = await engine.DeduzirEstoqueAsync(101);

            // Assert
            result.Sucesso.Should().BeFalse();
            result.Mensagem.Should().Contain("insuficiente");

            using var assertContext = GetDbContext(dbName);
            var pecaAtualizada = await assertContext.Pecas.FindAsync(2);
            pecaAtualizada!.QuantidadeEstoque.Should().Be(1);

            cacheMock.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Never);
        }


        [Fact]
        public async Task RestaurarEstoqueAsync_SemPecas_DeveRetornarSucessoEMensagem()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var ordem = new OrdemServico { Id = 103, ClienteId = 1, DataEntrada = DateTime.UtcNow };

            context.OrdensServico.Add(ordem);
            await context.SaveChangesAsync();

            var cacheMock = new Mock<IResilientCacheService>();
            var loggerMock = new Mock<ILogger<EstoqueEngine>>();
            var engine = new EstoqueEngine(context, cacheMock.Object, loggerMock.Object);

            // Act
            var result = await engine.RestaurarEstoqueAsync(103);

            // Assert
            result.Sucesso.Should().BeTrue();
            result.Mensagem.Should().Be("Nenhuma peça associada à Ordem de Serviço.");

            cacheMock.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Never);
        }
        [Fact]
        public async Task RestaurarEstoqueAsync_ComPecas_DeveAumentarEstoqueERetornarSucessoEInvalidarCache()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var peca = new Peca { Id = 3, Nome = "Conector USB-C", QuantidadeEstoque = 5, QuantidadeMinima = 3, ValorUnitario = 40 };
            var ordem = new OrdemServico { Id = 102, ClienteId = 1, DataEntrada = DateTime.UtcNow };
            var osPeca = new OrdemServicoPeca { Id = 3, OrdemServicoId = 102, PecaId = 3, Peca = peca, Quantidade = 5, ValorVenda = 40 };

            context.Pecas.Add(peca);
            context.OrdensServico.Add(ordem);
            context.OrdemServicoPecas.Add(osPeca);
            await context.SaveChangesAsync();

            var cacheMock = new Mock<IResilientCacheService>();
            var loggerMock = new Mock<ILogger<EstoqueEngine>>();
            var engine = new EstoqueEngine(context, cacheMock.Object, loggerMock.Object);

            // Act
            var result = await engine.RestaurarEstoqueAsync(102);

            // Assert
            result.Sucesso.Should().BeTrue();
            using var assertContext = GetDbContext(dbName);
            var pecaAtualizada = await assertContext.Pecas.FindAsync(3);
            pecaAtualizada!.QuantidadeEstoque.Should().Be(10);

            cacheMock.Verify(c => c.RemoveAsync("Estoque_AlertasEstoque"), Times.Once);
        }

        [Fact]
        public async Task ObterAlertasDeEstoqueAsync_ComCache_NaoDeveAcessarBanco()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            // To ensure the DB is not queried, we mock the cache to return a value
            var pecasEmCache = new List<Peca>
            {
                new Peca { Id = 1, Nome = "Peca 1", QuantidadeEstoque = 5, QuantidadeMinima = 10 }
            };

            var cacheMock = new Mock<IResilientCacheService>();
            cacheMock.Setup(c => c.GetAsync<List<Peca>>("Estoque_AlertasEstoque"))
                .ReturnsAsync(pecasEmCache);

            var loggerMock = new Mock<ILogger<EstoqueEngine>>();
            var engine = new EstoqueEngine(context, cacheMock.Object, loggerMock.Object);

            // Act
            var result = await engine.ObterAlertasDeEstoqueAsync();

            // Assert
            result.Should().BeEquivalentTo(pecasEmCache);
            cacheMock.Verify(c => c.GetAsync<List<Peca>>("Estoque_AlertasEstoque"), Times.Once);

            // Verifies SetAsync was not called.
            cacheMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<List<Peca>>(), It.IsAny<TimeSpan>()), Times.Never);
        }
        [Fact]
        public async Task ObterAlertasDeEstoqueAsync_SemCache_DeveBuscarDoBancoEPreencherCache()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            context.Pecas.AddRange(
                new Peca { Id = 10, Nome = "Peça Crítica 1", QuantidadeEstoque = 1, QuantidadeMinima = 5, ValorUnitario = 10 },
                new Peca { Id = 11, Nome = "Peça Crítica 2", QuantidadeEstoque = 0, QuantidadeMinima = 2, ValorUnitario = 20 },
                new Peca { Id = 12, Nome = "Peça Normal", QuantidadeEstoque = 20, QuantidadeMinima = 5, ValorUnitario = 30 }
            );
            await context.SaveChangesAsync();

            var cacheMock = new Mock<IResilientCacheService>();
            cacheMock.Setup(c => c.GetAsync<List<Peca>>("Estoque_AlertasEstoque"))
                .ReturnsAsync((List<Peca>?)null);

            var loggerMock = new Mock<ILogger<EstoqueEngine>>();
            var engine = new EstoqueEngine(context, cacheMock.Object, loggerMock.Object);

            // Act
            var alertas = await engine.ObterAlertasDeEstoqueAsync();

            // Assert
            alertas.Should().HaveCount(2);
            alertas.Select(p => p.Id).Should().Contain(new[] { 10, 11 });

            cacheMock.Verify(c => c.SetAsync("Estoque_AlertasEstoque", It.IsAny<List<Peca>>(), It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task ObterAlertasDeEstoqueAsync_ComCacheExistente_DeveRetornarDoCache()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var pecasEmCache = new List<Peca>
            {
                new Peca { Id = 99, Nome = "Peça Em Cache", QuantidadeEstoque = 1, QuantidadeMinima = 5 }
            };

            var cacheMock = new Mock<IResilientCacheService>();
            cacheMock.Setup(c => c.GetAsync<List<Peca>>("Estoque_AlertasEstoque"))
                .ReturnsAsync(pecasEmCache);

            var loggerMock = new Mock<ILogger<EstoqueEngine>>();
            var engine = new EstoqueEngine(context, cacheMock.Object, loggerMock.Object);

            // Act
            var alertas = await engine.ObterAlertasDeEstoqueAsync();

            // Assert
            alertas.Should().BeEquivalentTo(pecasEmCache);
            cacheMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<List<Peca>>(), It.IsAny<TimeSpan>()), Times.Never);
        }

        [Fact]
        public async Task CriarPecaAsync_ComDadosValidos_DeveSalvarPecaEInvalidarCache()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var cacheMock = new Mock<IResilientCacheService>();
            var loggerMock = new Mock<ILogger<EstoqueEngine>>();
            var engine = new EstoqueEngine(context, cacheMock.Object, loggerMock.Object);

            var dto = new PecaCreateDto
            {
                Nome = "Pasta Térmica Arctic Silver",
                QuantidadeEstoque = 15,
                QuantidadeMinima = 5,
                ValorUnitario = 65.50m
            };

            // Act
            var result = await engine.CriarPecaAsync(dto);

            // Assert
            result.Sucesso.Should().BeTrue();
            result.Peca.Should().NotBeNull();
            result.Peca!.Nome.Should().Be("Pasta Térmica Arctic Silver");

            using var assertContext = GetDbContext(dbName);
            var pecaSalva = await assertContext.Pecas.FirstOrDefaultAsync(p => p.Nome == "Pasta Térmica Arctic Silver");
            pecaSalva.Should().NotBeNull();
            pecaSalva!.QuantidadeEstoque.Should().Be(15);

            cacheMock.Verify(c => c.RemoveAsync("Estoque_AlertasEstoque"), Times.Once);
        }

        [Fact]
        public async Task AtualizarPecaAsync_ComPecaExistente_DeveAtualizarCamposEInvalidarCache()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var peca = new Peca { Id = 50, Nome = "SSD 500GB", QuantidadeEstoque = 10, QuantidadeMinima = 2, ValorUnitario = 200 };
            context.Pecas.Add(peca);
            await context.SaveChangesAsync();

            var cacheMock = new Mock<IResilientCacheService>();
            var loggerMock = new Mock<ILogger<EstoqueEngine>>();
            var engine = new EstoqueEngine(context, cacheMock.Object, loggerMock.Object);

            var dto = new PecaEditDto
            {
                Id = 50,
                Nome = "SSD 1TB NVMe",
                QuantidadeEstoque = 25,
                QuantidadeMinima = 5,
                ValorUnitario = 380
            };

            // Act
            var result = await engine.AtualizarPecaAsync(dto);

            // Assert
            result.Sucesso.Should().BeTrue();
            using var assertContext = GetDbContext(dbName);
            var pecaAtualizada = await assertContext.Pecas.FindAsync(50);
            pecaAtualizada!.Nome.Should().Be("SSD 1TB NVMe");
            pecaAtualizada.QuantidadeEstoque.Should().Be(25);
            pecaAtualizada.ValorUnitario.Should().Be(380);

            cacheMock.Verify(c => c.RemoveAsync("Estoque_AlertasEstoque"), Times.Once);
        }

        [Fact]
        public async Task AtualizarPecaAsync_ComIdInexistente_DeveRetornarFalha()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var cacheMock = new Mock<IResilientCacheService>();
            var loggerMock = new Mock<ILogger<EstoqueEngine>>();
            var engine = new EstoqueEngine(context, cacheMock.Object, loggerMock.Object);

            var dto = new PecaEditDto { Id = 999, Nome = "Inexistente" };

            // Act
            var result = await engine.AtualizarPecaAsync(dto);

            // Assert
            result.Sucesso.Should().BeFalse();
            result.Mensagem.Should().Contain("não encontrada");
            cacheMock.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExcluirPecaAsync_ComPecaLivre_DeveRemoverEInvalidarCache()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var peca = new Peca { Id = 70, Nome = "Cabo HDMI 2.1", QuantidadeEstoque = 10, QuantidadeMinima = 2, ValorUnitario = 50 };
            context.Pecas.Add(peca);
            await context.SaveChangesAsync();

            var cacheMock = new Mock<IResilientCacheService>();
            var loggerMock = new Mock<ILogger<EstoqueEngine>>();
            var engine = new EstoqueEngine(context, cacheMock.Object, loggerMock.Object);

            // Act
            var result = await engine.ExcluirPecaAsync(70);

            // Assert
            result.Sucesso.Should().BeTrue();
            using var assertContext = GetDbContext(dbName);
            var pecaExcluida = await assertContext.Pecas.FindAsync(70);
            pecaExcluida.Should().BeNull();

            cacheMock.Verify(c => c.RemoveAsync("Estoque_AlertasEstoque"), Times.Once);
        }

        [Fact]
        public async Task ExcluirPecaAsync_ComPecaVinculadaAOrdemServico_DeveRetornarFalhaEBloquearExclusao()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var peca = new Peca { Id = 80, Nome = "Teclado Dell Latitude", QuantidadeEstoque = 5, QuantidadeMinima = 1, ValorUnitario = 120 };
            var osPeca = new OrdemServicoPeca { Id = 1, OrdemServicoId = 500, PecaId = 80, Quantidade = 1, ValorVenda = 120 };

            context.Pecas.Add(peca);
            context.OrdemServicoPecas.Add(osPeca);
            await context.SaveChangesAsync();

            var cacheMock = new Mock<IResilientCacheService>();
            var loggerMock = new Mock<ILogger<EstoqueEngine>>();
            var engine = new EstoqueEngine(context, cacheMock.Object, loggerMock.Object);

            // Act
            var result = await engine.ExcluirPecaAsync(80);

            // Assert
            result.Sucesso.Should().BeFalse();
            result.Mensagem.ToLower().Should().Contain("não é possível excluir");

            using var assertContext = GetDbContext(dbName);
            var pecaAindaExiste = await assertContext.Pecas.FindAsync(80);
            pecaAindaExiste.Should().NotBeNull();

            cacheMock.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
