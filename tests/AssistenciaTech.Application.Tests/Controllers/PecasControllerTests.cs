using System;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class PecasControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly PecasController _controller;

        public PecasControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _controller = new PecasController(_context);
        }

        [Fact]
        public async Task Index_ReturnsViewResult_WithListOfPecas()
        {
            // Arrange
            _context.Pecas.Add(new Peca { Nome = "Peca 1", QuantidadeEstoque = 10, ValorUnitario = 50.0m });
            _context.Pecas.Add(new Peca { Nome = "Peca 2", QuantidadeEstoque = 20, ValorUnitario = 150.0m });
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<Peca>>().Subject;
            model.Should().HaveCount(2);
        }

        [Fact]
        public async Task Create_Post_ValidModel_ShouldAddPecaAndRedirectToIndex()
        {
            // Arrange
            var novaPeca = new PecaCreateDto
            {
                Nome = "Placa Mãe",
                QuantidadeEstoque = 10,
                ValorUnitario = 450.50m
            };

            // Act
            var result = await _controller.Create(novaPeca);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");

            var pecaInDb = await _context.Pecas.FirstOrDefaultAsync(p => p.Nome == "Placa Mãe");
            pecaInDb.Should().NotBeNull();
            pecaInDb.QuantidadeEstoque.Should().Be(10);
            pecaInDb.ValorUnitario.Should().Be(450.50m);
        }

        [Fact]
        public async Task Create_Post_InvalidModel_ShouldReturnViewWithModel_AndNotSaveToDb()
        {
            // Arrange
            var pecaInvalida = new PecaCreateDto
            {
                // Nome is required, leaving it empty or default
                QuantidadeEstoque = 10,
                ValorUnitario = 450.50m
            };

            _controller.ModelState.AddModelError("Nome", "O nome da peça é obrigatório.");

            // Act
            var result = await _controller.Create(pecaInvalida);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeEquivalentTo(pecaInvalida);

            var pecasInDb = await _context.Pecas.ToListAsync();
            pecasInDb.Should().BeEmpty(); // Nothing should have been saved
        }

        [Fact]
        public void Create_Get_ShouldReturnView()
        {
            // Act
            var result = _controller.Create();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }


        [Fact]
        public async Task DeleteConfirmed_ValidId_ShouldRemovePecaAndRedirectToIndex()
        {
            // Arrange
            var peca = new Peca
            {
                Nome = "Memória RAM 8GB",
                QuantidadeEstoque = 5,
                ValorUnitario = 150.00m
            };
            _context.Pecas.Add(peca);
            await _context.SaveChangesAsync();

            // Clear tracker to ensure FindAsync fetches from "DB"
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.DeleteConfirmed(peca.Id);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");

            var pecaInDb = await _context.Pecas.FindAsync(peca.Id);
            pecaInDb.Should().BeNull();
        }

        [Fact]
        public async Task DeleteConfirmed_InvalidId_ShouldNotRemoveAnythingAndRedirectToIndex()
        {
            // Arrange
            int invalidId = 999;
            var peca = new Peca
            {
                Nome = "HD 1TB",
                QuantidadeEstoque = 2,
                ValorUnitario = 200.00m
            };
            _context.Pecas.Add(peca);
            await _context.SaveChangesAsync();

            var initialCount = await _context.Pecas.CountAsync();

            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.DeleteConfirmed(invalidId);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");

            var currentCount = await _context.Pecas.CountAsync();
            currentCount.Should().Be(initialCount);
        }

        [Fact]
        public async Task Edit_Post_IdMismatch_ShouldReturnNotFound()
        {
            // Arrange
            int urlId = 1;
            var peca = new PecaEditDto
            {
                Id = 2,
                Nome = "Placa de Vídeo",
                QuantidadeEstoque = 5,
                ValorUnitario = 1500.00m
            };

            // Act
            var result = await _controller.Edit(urlId, peca);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Post_ValidModel_ShouldUpdatePecaAndRedirectToIndex()
        {
            // Arrange
            var peca = new Peca { Nome = "Processador Antigo", QuantidadeEstoque = 10, ValorUnitario = 500.00m };
            _context.Pecas.Add(peca);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var pecaAtualizada = new PecaEditDto
            {
                Id = peca.Id,
                Nome = "Processador Novo",
                QuantidadeEstoque = 15,
                ValorUnitario = 600.00m
            };

            // Act
            var result = await _controller.Edit(peca.Id, pecaAtualizada);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");

            var pecaInDb = await _context.Pecas.FindAsync(peca.Id);
            pecaInDb.Should().NotBeNull();
            pecaInDb.Nome.Should().Be("Processador Novo");
            pecaInDb.QuantidadeEstoque.Should().Be(15);
            pecaInDb.ValorUnitario.Should().Be(600.00m);
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ShouldReturnViewWithModel()
        {
            // Arrange
            var peca = new PecaEditDto { Id = 1, Nome = "Teclado", QuantidadeEstoque = 5, ValorUnitario = 100.00m };
            _controller.ModelState.AddModelError("Nome", "O nome é obrigatório");

            // Act
            var result = await _controller.Edit(1, peca);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeEquivalentTo(peca);
        }

        [Fact]
        public async Task Edit_Post_PecaNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var peca = new PecaEditDto { Id = 999, Nome = "Peça Inexistente" };

            // Act
            var result = await _controller.Edit(999, peca);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Post_ConcurrencyException_PecaDeleted_ShouldReturnNotFound()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var concurrencyContext = new ConcurrencyAppDbContext(options);
            using var controller = new PecasController(concurrencyContext);

            var peca = new Peca { Nome = "Placa de Rede", QuantidadeEstoque = 5, ValorUnitario = 50.00m };
            concurrencyContext.Pecas.Add(peca);
            await concurrencyContext.SaveChangesAsync();
            concurrencyContext.ChangeTracker.Clear();

            var pecaAtualizada = new PecaEditDto { Id = peca.Id, Nome = "Placa Wi-Fi", QuantidadeEstoque = 5, ValorUnitario = 60.00m };
            concurrencyContext.ThrowConcurrencyException = true;
            concurrencyContext.RemoveEntityOnException = true;

            // Act
            var result = await controller.Edit(peca.Id, pecaAtualizada);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Post_ConcurrencyException_PecaExists_ShouldThrow()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var concurrencyContext = new ConcurrencyAppDbContext(options);
            using var controller = new PecasController(concurrencyContext);

            var peca = new Peca { Nome = "Fonte 500W", QuantidadeEstoque = 2, ValorUnitario = 200.00m };
            concurrencyContext.Pecas.Add(peca);
            await concurrencyContext.SaveChangesAsync();
            concurrencyContext.ChangeTracker.Clear();

            var pecaAtualizada = new PecaEditDto { Id = peca.Id, Nome = "Fonte 600W", QuantidadeEstoque = 2, ValorUnitario = 250.00m };
            concurrencyContext.ThrowConcurrencyException = true;
            concurrencyContext.RemoveEntityOnException = false; // Entity still exists

            // Act & Assert
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => controller.Edit(peca.Id, pecaAtualizada));
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
