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
using Moq;
using Microsoft.AspNetCore.Mvc.Routing;

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
        public async Task Edit_WhenIdIsNull_ReturnsNotFound()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AppDbContext(options);
            var controller = new EquipamentoBackupController(context);
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.IsLocalUrl(It.IsAny<string>())).Returns(true);
            controller.Url = urlHelperMock.Object;

            // Act
            var result = await controller.Edit((int?)null);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
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
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.IsLocalUrl(It.IsAny<string>())).Returns(true);
            controller.Url = urlHelperMock.Object;

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
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.IsLocalUrl(It.IsAny<string>())).Returns(true);
            controller.Url = urlHelperMock.Object;
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
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.IsLocalUrl(It.IsAny<string>())).Returns(true);
            controller.Url = urlHelperMock.Object;

            var httpContext = new DefaultHttpContext();
            controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act
            var result = await controller.Devolver(1, returnUrl: null);

            // Assert
            var savedEquipamento = await context.EquipamentosBackup.FindAsync(1);
            savedEquipamento.Disponivel.Should().BeTrue();

            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
        }

        [Fact]
        public async Task Devolver_WhenEquipamentoExistsAndReturnUrlIsLocal_RedirectsToReturnUrl()
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
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.IsLocalUrl("/some-local-page")).Returns(true);
            controller.Url = urlHelperMock.Object;

            var httpContext = new DefaultHttpContext();
            controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act
            var result = await controller.Devolver(1, returnUrl: "/some-local-page");

            // Assert
            var savedEquipamento = await context.EquipamentosBackup.FindAsync(1);
            savedEquipamento.Disponivel.Should().BeTrue();

            var redirectResult = result.Should().BeOfType<LocalRedirectResult>().Subject;
            redirectResult.Url.Should().Be("/some-local-page");
        }

        [Fact]
        public async Task Devolver_WhenEquipamentoExistsAndReturnUrlIsNotLocal_RedirectsToIndex()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AppDbContext(options);
            var equipamento = new EquipamentoBackup { Id = 2, Descricao = "Test2", NumeroSerie = "1234", Disponivel = false };
            context.EquipamentosBackup.Add(equipamento);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var controller = new EquipamentoBackupController(context);
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.IsLocalUrl("http://malicious.com")).Returns(false);
            controller.Url = urlHelperMock.Object;

            var httpContext = new DefaultHttpContext();
            controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act
            var result = await controller.Devolver(2, returnUrl: "http://malicious.com");

            // Assert
            var savedEquipamento = await context.EquipamentosBackup.FindAsync(2);
            savedEquipamento.Disponivel.Should().BeTrue();

            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
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
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.IsLocalUrl(It.IsAny<string>())).Returns(true);
            controller.Url = urlHelperMock.Object;

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

        [Fact]
        public async Task Edit_Get_WhenEquipamentoNotFound_ReturnsNotFound()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AppDbContext(options);
            var controller = new EquipamentoBackupController(context);

            // Act
            var result = await controller.Edit(999);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Get_WhenEquipamentoExists_ReturnsViewResultWithEquipamento()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AppDbContext(options);
            var equipamento = new EquipamentoBackup { Descricao = "Notebook Dell", NumeroSerie = "SN12345", Disponivel = true };
            context.EquipamentosBackup.Add(equipamento);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var controller = new EquipamentoBackupController(context);

            // Act
            var result = await controller.Edit(equipamento.Id);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<EquipamentoBackup>().Subject;
            model.Id.Should().Be(equipamento.Id);
            model.Descricao.Should().Be("Notebook Dell");
        }

        [Fact]
        public async Task Edit_Post_WhenIdMismatchesEquipamentoId_ReturnsNotFound()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AppDbContext(options);
            var controller = new EquipamentoBackupController(context);
            var equipamento = new EquipamentoBackup { Id = 2, Descricao = "Notebook Dell", NumeroSerie = "SN12345" };

            // Act
            var result = await controller.Edit(1, equipamento);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Post_WhenModelStateIsInvalid_ReturnsViewResultWithEquipamento()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AppDbContext(options);
            var controller = new EquipamentoBackupController(context);
            var equipamento = new EquipamentoBackup { Id = 1, Descricao = "", NumeroSerie = "SN12345" };
            controller.ModelState.AddModelError("Descricao", "Descricao é obrigatória.");

            // Act
            var result = await controller.Edit(1, equipamento);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeEquivalentTo(equipamento);
        }

        [Fact]
        public async Task Edit_Post_WhenExistingEquipamentoNotFound_ReturnsNotFound()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AppDbContext(options);
            var controller = new EquipamentoBackupController(context);
            var equipamento = new EquipamentoBackup { Id = 999, Descricao = "Notebook Inexistente", NumeroSerie = "SN999" };

            // Act
            var result = await controller.Edit(999, equipamento);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Post_WhenValidModel_UpdatesEquipamentoAndRedirectsToIndex()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AppDbContext(options);
            var equipamento = new EquipamentoBackup { Descricao = "Notebook Lenovo", NumeroSerie = "SN001", Disponivel = true };
            context.EquipamentosBackup.Add(equipamento);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var controller = new EquipamentoBackupController(context);
            var updatedEquipamento = new EquipamentoBackup { Id = equipamento.Id, Descricao = "Notebook Lenovo ThinkPad", NumeroSerie = "SN001-UPDATED", Disponivel = true };

            // Act
            var result = await controller.Edit(equipamento.Id, updatedEquipamento);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");

            var equipamentoInDb = await context.EquipamentosBackup.FindAsync(equipamento.Id);
            equipamentoInDb.Should().NotBeNull();
            equipamentoInDb.Descricao.Should().Be("Notebook Lenovo ThinkPad");
            equipamentoInDb.NumeroSerie.Should().Be("SN001-UPDATED");
        }
    }
}
