using System;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using FluentAssertions;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class HomeControllerTests
    {

        [Fact]
        public void Index_ReturnsViewResult_WithServicosHome()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using (var context = new AppDbContext(options))
            {
                var controller = new HomeController(context, Mock.Of<ILogger<HomeController>>());

                // Act
                var result = controller.Index();

                // Assert
                var viewResult = result.Should().BeOfType<ViewResult>().Which;
                var servicos = viewResult.ViewData["ServicosHome"] as System.Collections.Generic.IEnumerable<dynamic>;

                servicos.Should().NotBeNull();

                var servicosList = System.Linq.Enumerable.ToList(servicos);
                servicosList.Should().HaveCount(3);

                // Use reflection to verify the properties of the first element due to anonymous type visibility
                var firstService = servicosList[0];
                var type = (Type)firstService.GetType();

                var tituloProperty = type.GetProperty("Titulo");
                tituloProperty.Should().NotBeNull();
                var tituloValue = tituloProperty.GetValue(firstService) as string;
                tituloValue.Should().Be("Formatação e Backup");
            }
        }

        [Fact]
        public void TestDb_Success_ReturnsSuccessMessage()
        {
            // Arrange
            // SQLite in-memory supports OpenConnection() well for tests without needing a real relational DB
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var context = new AppDbContext(options))
            {
                context.Database.EnsureCreated();
                var controller = new HomeController(context, Mock.Of<ILogger<HomeController>>());

                // Act
                var result = controller.TestDb();

                // Assert
                var contentResult = Assert.IsType<ContentResult>(result);
                contentResult.Content.Should().Be("Conexão com o banco de dados realizada com SUCESSO!");
            }
            connection.Close();
        }

        [Fact]
        public void TestDb_Exception_ReturnsErrorMessage()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=non_existent_host;Database=test;Username=test;Password=test")
                .Options;

            using (var context = new AppDbContext(options))
            {
                var controller = new HomeController(context, Mock.Of<ILogger<HomeController>>());

                // Act
                var result = controller.TestDb();

                // Assert
                var contentResult = Assert.IsType<ContentResult>(result);
                contentResult.Content.Should().Be("ERRO: Não foi possível conectar ao banco de dados.");
            }
        }
    }
}
