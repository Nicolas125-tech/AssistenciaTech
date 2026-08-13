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
        public async Task Edit_Get_NullId_ShouldReturnNotFound()
        {
            // Act
            var result = await _controller.Edit((int?)null);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Get_InvalidId_ShouldReturnNotFound()
        {
            // Act
            var result = await _controller.Edit(999);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Get_ValidId_ShouldReturnViewWithPeca()
        {
            // Arrange
            var peca = new Peca
            {
                Nome = "Processador",
                QuantidadeEstoque = 5,
                ValorUnitario = 1500.00m
            };
            _context.Pecas.Add(peca);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Edit(peca.Id);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<Peca>().Subject;
            model.Id.Should().Be(peca.Id);
            model.Nome.Should().Be(peca.Nome);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
