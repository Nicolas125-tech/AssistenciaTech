using System;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
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
        public async Task Create_Post_ValidModel_ShouldAddPecaAndRedirectToIndex()
        {
            // Arrange
            var novaPeca = new Peca
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
            var pecaInvalida = new Peca
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
        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
