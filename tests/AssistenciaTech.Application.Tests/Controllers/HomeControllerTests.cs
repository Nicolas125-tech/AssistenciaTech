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

                // Use reflection to verify the properties due to anonymous type visibility
                var firstService = servicosList[0];
                var type = (Type)firstService.GetType();

                // Servico 1
                (type.GetProperty("Titulo")?.GetValue(firstService) as string).Should().Be("Formatação e Backup");
                (type.GetProperty("Descricao")?.GetValue(firstService) as string).Should().Be("Instalação limpa do Windows com backup completo e seguro dos seus arquivos.");
                (type.GetProperty("Icone")?.GetValue(firstService) as string).Should().Be("bi-laptop");
                (type.GetProperty("Preco")?.GetValue(firstService) as string).Should().Be("A partir de R$ 120");

                // Servico 2
                var secondService = servicosList[1];
                (type.GetProperty("Titulo")?.GetValue(secondService) as string).Should().Be("Limpeza Preventiva");
                (type.GetProperty("Descricao")?.GetValue(secondService) as string).Should().Be("Limpeza interna profunda e troca de pasta térmica de alta performance.");
                (type.GetProperty("Icone")?.GetValue(secondService) as string).Should().Be("bi-tools");
                (type.GetProperty("Preco")?.GetValue(secondService) as string).Should().Be("A partir de R$ 150");

                // Servico 3
                var thirdService = servicosList[2];
                (type.GetProperty("Titulo")?.GetValue(thirdService) as string).Should().Be("Reparo de Placa-Mãe");
                (type.GetProperty("Descricao")?.GetValue(thirdService) as string).Should().Be("Conserto de curtos, troca de componentes e regravação de BIOS.");
                (type.GetProperty("Icone")?.GetValue(thirdService) as string).Should().Be("bi-motherboard");
                (type.GetProperty("Preco")?.GetValue(thirdService) as string).Should().Be("Sob Orçamento");
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

        [Fact]
        public void Servicos_ReturnsViewResult()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using (var context = new AppDbContext(options))
            {
                var controller = new HomeController(context, Mock.Of<ILogger<HomeController>>());

                // Act
                var result = controller.Servicos();

                // Assert
                result.Should().BeOfType<ViewResult>();
            }
        }

        [Fact]
        public void Error_WhenActivityCurrentIsNull_ReturnsViewResultWithTraceIdentifier()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using (var context = new AppDbContext(options))
            {
                var controller = new HomeController(context, Mock.Of<ILogger<HomeController>>());
                var expectedTraceId = "TestTraceIdentifier_12345";
                controller.ControllerContext = new ControllerContext
                {
                    HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { TraceIdentifier = expectedTraceId }
                };

                // Ensure Activity.Current is null
                if (System.Diagnostics.Activity.Current != null)
                {
                    System.Diagnostics.Activity.Current.Stop();
                }

                // Act
                var result = controller.Error();

                // Assert
                var viewResult = result.Should().BeOfType<ViewResult>().Which;
                var model = viewResult.Model.Should().BeOfType<AssistenciaTech.Models.ErrorViewModel>().Which;
                model.RequestId.Should().Be(expectedTraceId);
                model.ShowRequestId.Should().BeTrue();
            }
        }

        [Fact]
        public void Error_WhenActivityCurrentIsNotNull_ReturnsViewResultWithActivityId()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using (var context = new AppDbContext(options))
            {
                var controller = new HomeController(context, Mock.Of<ILogger<HomeController>>());
                controller.ControllerContext = new ControllerContext
                {
                    HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { TraceIdentifier = "FallbackTraceId" }
                };

                var activity = new System.Diagnostics.Activity("TestActivity");
                activity.Start();

                try
                {
                    // Act
                    var result = controller.Error();

                    // Assert
                    var viewResult = result.Should().BeOfType<ViewResult>().Which;
                    var model = viewResult.Model.Should().BeOfType<AssistenciaTech.Models.ErrorViewModel>().Which;
                    model.RequestId.Should().Be(activity.Id);
                    model.ShowRequestId.Should().BeTrue();
                }
                finally
                {
                    activity.Stop();
                }
            }
        }

        [Theory]
        [InlineData("Req-123", true)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void ErrorViewModel_ShowRequestId_ReturnsCorrectValue(string? requestId, bool expectedShowRequestId)
        {
            // Arrange & Act
            var model = new AssistenciaTech.Models.ErrorViewModel { RequestId = requestId };

            // Assert
            model.ShowRequestId.Should().Be(expectedShowRequestId);
        }
    }
}
