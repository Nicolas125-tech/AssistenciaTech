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
