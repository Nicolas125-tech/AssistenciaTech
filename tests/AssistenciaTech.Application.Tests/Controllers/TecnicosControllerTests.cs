using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.DTOs;
using AssistenciaTech.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class TecnicosControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly TecnicosController _controller;

        public TecnicosControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _controller = new TecnicosController(_context);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithListOfTecnicos()
        {
            // Arrange
            var tecnico1 = new Tecnico { Nome = "Tecnico 1", PercentualComissao = 10, Ativo = true };
            var tecnico2 = new Tecnico { Nome = "Tecnico 2", PercentualComissao = 20, Ativo = false };

            _context.Tecnicos.AddRange(tecnico1, tecnico2);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<Tecnico>>().Subject;

            model.Should().HaveCount(2);
            model.Should().ContainSingle(t => t.Nome == "Tecnico 1");
            model.Should().ContainSingle(t => t.Nome == "Tecnico 2");
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
        public async Task Create_Post_ValidModel_ShouldAddTecnicoAndRedirectToIndex()
        {
            // Arrange
            var novoTecnicoDto = new TecnicoCreateDto
            {
                Nome = "Novo Tecnico",
                PercentualComissao = 15.5m,
                Ativo = true
            };

            // Act
            var result = await _controller.Create(novoTecnicoDto);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");

            var tecnicoInDb = await _context.Tecnicos.FirstOrDefaultAsync(t => t.Nome == "Novo Tecnico");
            tecnicoInDb.Should().NotBeNull();
            tecnicoInDb.PercentualComissao.Should().Be(15.5m);
            tecnicoInDb.Ativo.Should().BeTrue();
        }

        [Fact]
        public async Task Create_Post_InvalidModel_ShouldReturnViewWithModel_AndNotSaveToDb()
        {
            // Arrange
            var tecnicoInvalidoDto = new TecnicoCreateDto
            {
                // Nome is missing
                PercentualComissao = 15.5m,
                Ativo = true
            };

            _controller.ModelState.AddModelError("Nome", "O nome do técnico é obrigatório.");

            // Act
            var result = await _controller.Create(tecnicoInvalidoDto);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeEquivalentTo(tecnicoInvalidoDto);

            var tecnicosInDb = await _context.Tecnicos.ToListAsync();
            tecnicosInDb.Should().BeEmpty();
        }


        [Fact]
        public async Task DeleteConfirmed_WithExistingId_ShouldRemoveTecnicoAndRedirectToIndex()
        {
            // Arrange
            var tecnico = new Tecnico
            {
                Nome = "Tecnico to delete",
                PercentualComissao = 10,
                Ativo = true
            };
            _context.Tecnicos.Add(tecnico);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.DeleteConfirmed(tecnico.Id);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");

            var tecnicoInDb = await _context.Tecnicos.FindAsync(tecnico.Id);
            tecnicoInDb.Should().BeNull();
        }

        [Fact]
        public async Task DeleteConfirmed_WithNonExistingId_ShouldRedirectToIndexAndNotThrow()
        {
            // Arrange
            var nonExistingId = 999;

            // Act
            var result = await _controller.DeleteConfirmed(nonExistingId);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
