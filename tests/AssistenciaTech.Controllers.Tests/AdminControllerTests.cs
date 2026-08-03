using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AssistenciaTech.Controllers.Tests
{
    public class AdminControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IEstoqueService> _mockEstoqueService;
        private readonly Mock<IWebHostEnvironment> _mockEnv;

        public AdminControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new TestAppDbContext(options);
            _mockEstoqueService = new Mock<IEstoqueService>();
            _mockEnv = new Mock<IWebHostEnvironment>();
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public async Task Edit_Post_WhenExceptionThrown_ShouldReturnViewWithModelError()
        {
            // Arrange
            var os = new OrdemServico { Id = 1, Equipamento = "Note", ClienteId = 1, Status = "Recebido" };
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            // Set the flag to throw exception on SaveChangesAsync
            ((TestAppDbContext)_context).ThrowOnSaveChanges = true;

            var controller = new AdminController(_context, _mockEstoqueService.Object, _mockEnv.Object);

            var formFiles = new FormFileCollection();

            var updatedOs = new OrdemServico { Id = 1, Equipamento = "Note", ClienteId = 1, Status = "Em Analise" };

            // Act
            var result = await controller.Edit(1, updatedOs, formFiles);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeEquivalentTo(updatedOs);

            controller.ModelState.IsValid.Should().BeFalse();
            controller.ModelState.ErrorCount.Should().Be(1);
            controller.ModelState.Values.SelectMany(v => v.Errors)
                .Should().ContainSingle(e => e.ErrorMessage == "Ocorreu um erro ao atualizar os dados.");
        }
    }

    // Custom DbContext for testing to allow overriding SaveChangesAsync
    public class TestAppDbContext : AppDbContext
    {
        public bool ThrowOnSaveChanges { get; set; }

        public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            if (ThrowOnSaveChanges)
            {
                throw new Exception("Test exception");
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
