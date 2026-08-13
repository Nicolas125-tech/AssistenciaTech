using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Xunit;

using System.Data.Common;

namespace AssistenciaTech.Application.Tests
{
    public class AdminControllerTests
    {
        [Fact]
        public async Task Index_WhenDashboardServiceThrowsException_ReturnsViewWithEmptyListAndErrorViewBag()
        {
            // Arrange
            var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AppDbContext(dbContextOptions);

            var mockEstoqueService = new Mock<IEstoqueService>();
            var mockEnv = new Mock<IWebHostEnvironment>();
            var mockPdfService = new Mock<IPdfGeneratorService>();
            var mockDashboardService = new Mock<IAdminDashboardService>();

            mockDashboardService
                .Setup(s => s.GetDashboardDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ThrowsAsync(new TestDbException("Database connection failed"));

            var mockLogger = new Mock<ILogger<AdminController>>();
            var mockEquipamentoBackupService = new Mock<IEquipamentoBackupService>();
            var controller = new AdminController(context, mockEstoqueService.Object, mockEnv.Object, mockPdfService.Object, mockDashboardService.Object, mockEquipamentoBackupService.Object, mockLogger.Object);

            // Set TempData
            var httpContext = new DefaultHttpContext();
            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            controller.TempData = tempData;

            // Act
            var result = await controller.Index(string.Empty, string.Empty);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<OrdemServico>>().Subject;
            model.Should().BeEmpty();

            var viewBag = controller.ViewBag;
            string erroBanco = viewBag.ErroBanco;
            erroBanco.Should().Be("Erro ao conectar ao banco de dados. Por favor, tente novamente mais tarde.");

            int totalAbertas = viewBag.TotalAbertas;
            totalAbertas.Should().Be(0);

            int equipamentosProntos = viewBag.EquipamentosProntos;
            equipamentosProntos.Should().Be(0);

            decimal faturamentoPrevisto = viewBag.FaturamentoPrevisto;
            faturamentoPrevisto.Should().Be(0m);
        }
    }

    public class TestDbException : DbException
    {
        public TestDbException(string message) : base(message) { }
    }
}