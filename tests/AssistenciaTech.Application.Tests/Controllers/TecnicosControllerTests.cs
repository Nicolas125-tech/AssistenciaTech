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
        public async Task Edit_Get_ValidId_ShouldReturnViewWithTecnicoDto()
        {
            // Arrange
            var tecnico = new Tecnico { Nome = "Tecnico Edit", PercentualComissao = 12.5m, Ativo = true };
            _context.Tecnicos.Add(tecnico);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.Edit(tecnico.Id);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<TecnicoUpdateDto>().Subject;

            model.Id.Should().Be(tecnico.Id);
            model.Nome.Should().Be(tecnico.Nome);
            model.PercentualComissao.Should().Be(tecnico.PercentualComissao);
            model.Ativo.Should().BeTrue();
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
        public async Task Edit_Post_ValidModel_ShouldUpdateTecnicoAndRedirectToIndex()
        {
            // Arrange
            var tecnico = new Tecnico { Nome = "Tecnico Antigo", PercentualComissao = 10m, Ativo = true };
            _context.Tecnicos.Add(tecnico);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var tecnicoUpdateDto = new TecnicoUpdateDto
            {
                Id = tecnico.Id,
                Nome = "Tecnico Novo",
                PercentualComissao = 15m,
                Ativo = false
            };

            // Act
            var result = await _controller.Edit(tecnico.Id, tecnicoUpdateDto);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");

            var tecnicoInDb = await _context.Tecnicos.FindAsync(tecnico.Id);
            tecnicoInDb.Should().NotBeNull();
            tecnicoInDb.Nome.Should().Be("Tecnico Novo");
            tecnicoInDb.PercentualComissao.Should().Be(15m);
            tecnicoInDb.Ativo.Should().BeFalse();
        }

        [Fact]
        public async Task Edit_Post_IdMismatch_ShouldReturnNotFound()
        {
            // Arrange
            var tecnicoUpdateDto = new TecnicoUpdateDto { Id = 1, Nome = "Tecnico" };

            // Act
            var result = await _controller.Edit(2, tecnicoUpdateDto);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ShouldReturnViewWithModel_AndNotSaveToDb()
        {
            // Arrange
            var tecnico = new Tecnico { Nome = "Tecnico Antigo", PercentualComissao = 10m, Ativo = true };
            _context.Tecnicos.Add(tecnico);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var tecnicoUpdateDto = new TecnicoUpdateDto
            {
                Id = tecnico.Id,
                // Nome is missing, but it's required
                PercentualComissao = 15m,
                Ativo = false
            };

            _controller.ModelState.AddModelError("Nome", "O nome do técnico é obrigatório.");

            // Act
            var result = await _controller.Edit(tecnico.Id, tecnicoUpdateDto);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeEquivalentTo(tecnicoUpdateDto);

            var tecnicoInDb = await _context.Tecnicos.FindAsync(tecnico.Id);
            tecnicoInDb.Nome.Should().Be("Tecnico Antigo"); // Should not have changed
        }

        [Fact]
        public async Task Edit_Post_TecnicoNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var tecnicoUpdateDto = new TecnicoUpdateDto { Id = 999, Nome = "Tecnico", PercentualComissao = 10m, Ativo = true };

            // Act
            var result = await _controller.Edit(999, tecnicoUpdateDto);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }


        [Fact]
        public async Task Edit_Post_ConcurrencyException_TecnicoDeleted_ShouldReturnNotFound()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var concurrencyContext = new ConcurrencyAppDbContext(options);
            using var controller = new TecnicosController(concurrencyContext);

            var tecnico = new Tecnico { Nome = "Tecnico Old", PercentualComissao = 10, Ativo = true };
            concurrencyContext.Tecnicos.Add(tecnico);
            await concurrencyContext.SaveChangesAsync();
            concurrencyContext.ChangeTracker.Clear();

            var tecnicoUpdateDto = new TecnicoUpdateDto { Id = tecnico.Id, Nome = "Tecnico Novo", PercentualComissao = 15, Ativo = false };
            concurrencyContext.ThrowConcurrencyException = true;
            concurrencyContext.RemoveEntityOnException = true;

            // Act
            var result = await controller.Edit(tecnico.Id, tecnicoUpdateDto);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Post_ConcurrencyException_TecnicoExists_ShouldThrow()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var concurrencyContext = new ConcurrencyAppDbContext(options);
            using var controller = new TecnicosController(concurrencyContext);

            var tecnico = new Tecnico { Nome = "Tecnico Old", PercentualComissao = 10, Ativo = true };
            concurrencyContext.Tecnicos.Add(tecnico);
            await concurrencyContext.SaveChangesAsync();
            concurrencyContext.ChangeTracker.Clear();

            var tecnicoUpdateDto = new TecnicoUpdateDto { Id = tecnico.Id, Nome = "Tecnico Novo", PercentualComissao = 15, Ativo = false };
            concurrencyContext.ThrowConcurrencyException = true;
            concurrencyContext.RemoveEntityOnException = false; // Entity still exists

            // Act & Assert
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => controller.Edit(tecnico.Id, tecnicoUpdateDto));
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
