using Microsoft.AspNetCore.Http;
using System;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;

namespace AssistenciaTech.Application.Tests
{
    public class EquipamentoBackupControllerTests
    {
        private class ConcurrencyThrowingDbContext : AppDbContext
        {
            public ConcurrencyThrowingDbContext(DbContextOptions<AppDbContext> options) : base(options)
            {
            }

            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                throw new DbUpdateConcurrencyException();
            }
        }

        [Fact]
        public async Task Edit_WhenConcurrencyExceptionAndEquipamentoDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new ConcurrencyThrowingDbContext(options);
            var controller = new EquipamentoBackupController(context);

            var equipamento = new EquipamentoBackup { Id = 1, Descricao = "Test", NumeroSerie = "123", Disponivel = true };

            // Act
            var result = await controller.Edit(1, equipamento);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_WhenConcurrencyExceptionAndEquipamentoExists_ThrowsConcurrencyException()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new ConcurrencyThrowingDbContext(options);

            // Add existing item using synchronous SaveChanges so it bypasses our overridden SaveChangesAsync
            context.EquipamentosBackup.Add(new EquipamentoBackup { Id = 1, Descricao = "Old", NumeroSerie = "123", Disponivel = true });
            context.SaveChanges();

            // Detach to allow the controller to update the entity with the same ID
            context.ChangeTracker.Clear();

            var controller = new EquipamentoBackupController(context);
            var equipamento = new EquipamentoBackup { Id = 1, Descricao = "New", NumeroSerie = "123", Disponivel = true };

            // Act
            Func<Task> action = async () => await controller.Edit(1, equipamento);

            // Assert
            await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
        }

        [Fact]
        public async Task Devolver_WhenEquipamentoExists_SetsDisponivelToTrueAndRedirectsToIndex()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AppDbContext(options);
            var equipamento = new EquipamentoBackup { Id = 1, Descricao = "Test", NumeroSerie = "123", Disponivel = false };
            context.EquipamentosBackup.Add(equipamento);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var controller = new EquipamentoBackupController(context);

            // Mock HttpContext to simulate missing Referer header
            var httpContext = new DefaultHttpContext();
            controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act
            var result = await controller.Devolver(1);

            // Assert
            var savedEquipamento = await context.EquipamentosBackup.FindAsync(1);
            savedEquipamento.Disponivel.Should().BeTrue();

            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
        }

        [Fact]
        public async Task Devolver_WhenEquipamentoExistsAndRefererPresent_RedirectsToReferer()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AppDbContext(options);
            var equipamento = new EquipamentoBackup { Id = 1, Descricao = "Test", NumeroSerie = "123", Disponivel = false };
            context.EquipamentosBackup.Add(equipamento);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var controller = new EquipamentoBackupController(context);

            // Mock HttpContext to simulate Referer header
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Referer"] = "https://example.com/somepage";
            controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act
            var result = await controller.Devolver(1);

            // Assert
            var savedEquipamento = await context.EquipamentosBackup.FindAsync(1);
            savedEquipamento.Disponivel.Should().BeTrue();

            var redirectResult = result.Should().BeOfType<RedirectResult>().Subject;
            redirectResult.Url.Should().Be("https://example.com/somepage");
        }

        [Fact]
        public async Task Devolver_WhenEquipamentoDoesNotExist_GracefullyRedirects()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AppDbContext(options);
            var controller = new EquipamentoBackupController(context);

            // Mock HttpContext
            var httpContext = new DefaultHttpContext();
            controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act
            var result = await controller.Devolver(999);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
        }
    }
}
