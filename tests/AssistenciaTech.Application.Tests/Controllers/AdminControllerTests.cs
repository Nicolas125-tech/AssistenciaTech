using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class TestDbException : System.Data.Common.DbException
    {
        public TestDbException(string message) : base(message) { }
    }

    public class AdminControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IEstoqueService> _mockEstoqueService;
        private readonly Mock<IWebHostEnvironment> _mockEnv;
        private readonly Mock<IPdfGeneratorService> _mockPdfGeneratorService;
        private readonly Mock<IAdminDashboardService> _mockDashboardService;
        private readonly Mock<ILogger<AdminController>> _mockLogger;
        private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
        private readonly AdminController _controller;

        public AdminControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _mockEstoqueService = new Mock<IEstoqueService>();
            _mockEnv = new Mock<IWebHostEnvironment>();
            _mockPdfGeneratorService = new Mock<IPdfGeneratorService>();
            _mockDashboardService = new Mock<IAdminDashboardService>();
            _mockLogger = new Mock<ILogger<AdminController>>();
            _mockScopeFactory = new Mock<IServiceScopeFactory>();

            // Setup ScopeFactory to return a scope containing the DbContext
            var mockScope = new Mock<IServiceScope>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(sp => sp.GetService(typeof(AppDbContext))).Returns(_context);
            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
            _mockScopeFactory.Setup(s => s.CreateScope()).Returns(mockScope.Object);

            var _mockEquipamentoBackupService = new Mock<IEquipamentoBackupService>();
            _controller = new AdminController(
                _context,
                _mockEstoqueService.Object,
                _mockEnv.Object,
                _mockPdfGeneratorService.Object,
                _mockDashboardService.Object,
                _mockEquipamentoBackupService.Object,
                _mockLogger.Object,
                _mockScopeFactory.Object
            );
        }


        [Fact]
        public async Task Index_Get_ReturnsEmptyList_WhenDatabaseFails()
        {
            // Arrange

            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            _controller.TempData = tempData;
            _mockDashboardService
                .Setup(s => s.GetDashboardDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ThrowsAsync(new TestDbException("Simulated DB connection error"));

            // Act
            var result = await _controller.Index(null, null, 1) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result.Model.Should().BeEquivalentTo(new List<OrdemServico>());

            var viewData = result.ViewData;
            viewData["ErroBanco"].Should().Be("Erro ao conectar ao banco de dados. Por favor, tente novamente mais tarde.");
            viewData["TotalAbertas"].Should().Be(0);
            viewData["EquipamentosProntos"].Should().Be(0);
            viewData["FaturamentoPrevisto"].Should().Be(0m);
        }

        [Fact]
        public async Task ExportarCsv_DeveEscreverNoResponseBodyOArquivoCsvComDadosDasOrdensDeServico()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Cliente Teste", Email = "teste@teste.com", Telefone = "12345678" };
            _context.Clientes.Add(cliente);

            var os1 = new OrdemServico
            {
                Id = 1,
                ClienteId = 1,
                Cliente = cliente,
                Equipamento = "PC Gamer",
                DataEntrada = new DateTime(2023, 10, 01),
                Status = "Orçamento",
                CustoPecas = 100,
                CustoMaoDeObra = 50,
                DescontoAplicado = 10,
                ValorOrcamento = 140
            };
            var os2 = new OrdemServico
            {
                Id = 2,
                ClienteId = 1,
                Cliente = cliente,
                Equipamento = "Notebook",
                DataEntrada = new DateTime(2023, 10, 05),
                Status = "Concluído",
                CustoPecas = 0,
                CustoMaoDeObra = 200,
                DescontoAplicado = 0,
                ValorOrcamento = 200
            };

            _context.OrdensServico.AddRange(os1, os2);
            await _context.SaveChangesAsync();

            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            var responseBody = new System.IO.MemoryStream();
            httpContext.Response.Body = responseBody;

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            await _controller.ExportarCsv();

            // Assert
            httpContext.Response.ContentType.Should().Be("text/csv");
            httpContext.Response.Headers["Content-Disposition"].ToString().Should().Be("attachment; filename=\"OrdensDeServico.csv\"");

            var csvString = System.Text.Encoding.UTF8.GetString(responseBody.ToArray());
            var linhas = csvString.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            linhas.Should().HaveCount(3); // Cabecalho + 2 OS
            linhas[0].Should().Be("Id,Cliente,Equipamento,Data Entrada,Status,Valor Orçamento");

            // Ordem decrescente por ID
            linhas[1].Should().Contain("2,\"Cliente Teste\",\"Notebook\",05/10/2023,Concluído,200");
            linhas[2].Should().Contain("1,\"Cliente Teste\",\"PC Gamer\",01/10/2023,Orçamento,140");
        }

        [Fact]
        public async Task ExportarCsv_DeveFazerFlushEmLotes_QuandoMuitasOrdensServicoExistem()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Cliente Teste", Email = "teste@teste.com", Telefone = "12345678" };
            _context.Clientes.Add(cliente);

            var ordens = new List<OrdemServico>();
            for (int i = 1; i <= 105; i++)
            {
                ordens.Add(new OrdemServico
                {
                    Id = i,
                    ClienteId = 1,
                    Cliente = cliente,
                    Equipamento = $"Equipamento {i}",
                    DataEntrada = new DateTime(2023, 10, 01),
                    Status = "Orçamento",
                    ValorOrcamento = 100 + i
                });
            }

            _context.OrdensServico.AddRange(ordens);
            await _context.SaveChangesAsync();

            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            var responseBody = new System.IO.MemoryStream();
            httpContext.Response.Body = responseBody;

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            await _controller.ExportarCsv();

            // Assert
            var csvString = System.Text.Encoding.UTF8.GetString(responseBody.ToArray());
            var linhas = csvString.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            linhas.Should().HaveCount(106); // Cabecalho + 105 OS
            linhas[0].Should().Be("Id,Cliente,Equipamento,Data Entrada,Status,Valor Orçamento");

            // A ordem é decrescente, então a primeira linha de dados é a do ID 105
            linhas[1].Should().Contain("105,\"Cliente Teste\",\"Equipamento 105\",01/10/2023,Orçamento,205");
            // E a última linha de dados é a do ID 1
            linhas[105].Should().Contain("1,\"Cliente Teste\",\"Equipamento 1\",01/10/2023,Orçamento,101");
        }

        [Fact]
        public async Task Create_DeveConfigurarTempDataAlertaGarantia_QuandoOrdemComMesmoNumeroSerieExisteHaMenosDe30Dias()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Cliente Teste", Email = "teste@teste.com", Telefone = "12345678", Cpf = "12345678901" };
            _context.Clientes.Add(cliente);

            var osExistente = new OrdemServico
            {
                Id = 1,
                ClienteId = 1,
                Equipamento = "PC Gamer Antigo",
                NumeroSerie = "SN123456",
                DataEntrada = DateTime.UtcNow.AddDays(-10), // Menos de 30 dias
                Status = WorkflowStatus.Concluido,
                ValorOrcamento = 100
            };
            _context.OrdensServico.Add(osExistente);
            await _context.SaveChangesAsync();

            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            _controller.TempData = tempData;

            var novaOs = new OrdemServico
            {
                Id = 2,
                ClienteId = 1,
                Equipamento = "PC Gamer Novo",
                NumeroSerie = "SN123456", // Mesmo número de série
                CustoPecas = 100,
                CustoMaoDeObra = 100
            };

            // Act
            var result = await _controller.Create(novaOs) as RedirectToActionResult;

            // Assert
            result.Should().NotBeNull();
            result.ActionName.Should().Be("Index");
            _controller.TempData.Should().ContainKey("AlertaGarantia");
            _controller.TempData["AlertaGarantia"].ToString().Should().Contain("ATENÇÃO: O equipamento com NS SN123456 já deu entrada na assistência nos últimos 30 dias. Verifique se é um retorno em garantia!");
        }


        [Theory]
        [InlineData("test.jpg", "image/jpeg")]
        [InlineData("test.jpeg", "image/jpeg")]
        [InlineData("test.png", "image/png")]
        [InlineData("test.gif", "image/gif")]
        [InlineData("test.pdf", "application/pdf")]
        public void GetEvidencia_ValidFile_ReturnsPhysicalFileResult_ForVariousExtensions(string fileName, string expectedContentType)
        {
            // Arrange
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var uploadsFolder = Path.Combine(tempPath, "SecureUploads", "Evidencias");
            Directory.CreateDirectory(uploadsFolder);
            var filePath = Path.Combine(uploadsFolder, fileName);
            System.IO.File.WriteAllText(filePath, "dummy content");

            _mockEnv.Setup(e => e.ContentRootPath).Returns(tempPath);

            // Act
            var result = _controller.GetEvidencia(fileName) as PhysicalFileResult;

            // Assert
            result.Should().NotBeNull();
            result.ContentType.Should().Be(expectedContentType);
            result.FileName.Should().Be(filePath);

            // Cleanup
            Directory.Delete(tempPath, true);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void GetEvidencia_NullOrEmptyFileName_ReturnsBadRequest(string? fileName)
        {
            // Act
            var result = _controller.GetEvidencia(fileName);

            // Assert
            result.Should().BeOfType<BadRequestResult>();
        }

        [Theory]
        [InlineData("..file.jpg")]
        [InlineData("file/name.jpg")]
        [InlineData("file\\name.jpg")]
        public void GetEvidencia_InvalidCharactersInFileName_ReturnsBadRequest(string fileName)
        {
            // Act
            var result = _controller.GetEvidencia(fileName);

            // Assert
            result.Should().BeOfType<BadRequestResult>();
        }

        [Theory]
        [InlineData("test.txt")]
        [InlineData("test.exe")]
        [InlineData("test")]
        public void GetEvidencia_InvalidExtension_ReturnsBadRequest(string fileName)
        {
            // Act
            var result = _controller.GetEvidencia(fileName);

            // Assert
            result.Should().BeOfType<BadRequestResult>();
        }

        [Fact]
        public void GetEvidencia_FileDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _mockEnv.Setup(e => e.ContentRootPath).Returns(tempPath);

            // To ensure the path checks pass (directory separator, starts with), we need the uploads directory to exist
            var uploadsFolder = Path.Combine(tempPath, "SecureUploads", "Evidencias");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = "not_found.jpg";

            // Act
            var result = _controller.GetEvidencia(fileName);

            // Assert
            result.Should().BeOfType<NotFoundResult>();

            // Cleanup
            Directory.Delete(tempPath, true);
        }

        [Fact]
        public async Task Edit_Get_ReturnsNotFound_WhenIdIsNull()
        {
            // Act
            var result = await _controller.Edit((int?)null);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Get_ReturnsNotFound_WhenOrdemServicoDoesNotExist()
        {
            // Act
            var result = await _controller.Edit(999);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Get_ReturnsViewResult_WithOrdemServico_WhenIdExists()
        {
            // Arrange
            var cliente = new Cliente { Nome = "Cliente Teste", Email = "teste@teste.com", Telefone = "12345678", Cpf = "12345678901" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var os = new OrdemServico { ClienteId = cliente.Id, Status = "Orçamento", Equipamento = "PC" };
            _context.OrdensServico.Add(os);

            var tecnico = new Tecnico { Nome = "Tecnico 1", Ativo = true };
            _context.Tecnicos.Add(tecnico);

            var equipamentoBackup = new EquipamentoBackup { Descricao = "Backup 1", Disponivel = true };
            _context.EquipamentosBackup.Add(equipamentoBackup);

            var contrato = new Contrato { ClienteId = cliente.Id, HorasSLA = 10, ValorMensal = 100 };
            _context.Contratos.Add(contrato);

            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Edit(os.Id);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<OrdemServico>().Subject;
            model.Id.Should().Be(os.Id);

            // Verify ViewBags
            var viewData = viewResult.ViewData;
            viewData["Tecnicos"].Should().BeOfType<SelectList>();
            viewData["EquipamentosBackup"].Should().BeOfType<SelectList>();
            viewData["Contratos"].Should().BeOfType<SelectList>();
        }

        [Fact]
        public async Task Edit_Get_ReturnsRedirectToIndex_WhenDatabaseFails()
        {
            // Arrange
            // We set up a new controller where TempData throws to simulate an issue, but wait,
            // the exception is caught in the controller
            // The catch block uses TempData. We need TempData to be initialized!
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            _controller.TempData = tempData;

            // We dispose the context so that accessing the DB throws an exception
            _context.Dispose();

            // Act
            var result = await _controller.Edit(1) as RedirectToActionResult;

            // Assert
            result.Should().NotBeNull();
            result.ActionName.Should().Be("Index");
            _controller.TempData["ErroBanco"].Should().Be("Não foi possível carregar a tela de edição. O banco de dados está inacessível.");
        }


        [Fact]
        public async Task ImprimirOs_ReturnsNotFound_WhenOrdemServicoDoesNotExist()
        {
            // Act
            var result = await _controller.ImprimirOs(999);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task ImprimirOs_ReturnsFileResult_WithPdf_WhenOrdemServicoExists()
        {
            // Arrange
            var cliente = new Cliente { Nome = "Cliente Teste", Email = "teste@teste.com", Telefone = "12345678", Cpf = "12345678901" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var os = new OrdemServico { ClienteId = cliente.Id, Status = "Orçamento", Equipamento = "PC" };
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var dummyPdfBytes = new byte[] { 1, 2, 3 };
            _mockPdfGeneratorService.Setup(s => s.GenerateOsPdf(It.IsAny<OrdemServico>()))
                                    .Returns(dummyPdfBytes);

            // Act
            var result = await _controller.ImprimirOs(os.Id);

            // Assert
            var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
            fileResult.ContentType.Should().Be("application/pdf");
            fileResult.FileDownloadName.Should().Be($"OS_{os.Id}_{cliente.Nome}.pdf");
            fileResult.FileContents.Should().BeEquivalentTo(dummyPdfBytes);
        }


        [Fact]
        public async Task Delete_OSComDependencias_DeveRetornarRedirectComErro()
        {
            // Arrange
            var cliente = new Cliente { Nome = "Teste Cliente", Email = "teste@teste.com", Telefone = "12345678" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var os = new OrdemServico
            {
                ClienteId = cliente.Id,
                Equipamento = "Notebook",
                ProblemaRelatado = "Tela Quebrada",
                Status = "Recebido"
            };

            // Adicionar uma evidência para gerar dependência
            os.Evidencias.Add(new Evidencia { CaminhoArquivo = "caminho/teste.jpg" });

            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Delete(os.Id);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
            redirectResult.RouteValues.Should().ContainKey("erro");
            redirectResult.RouteValues["erro"].ToString().Should().Contain("dependências");

            // Verifica que não foi deletado
            var osNoBanco = await _context.OrdensServico.FindAsync(os.Id);
            osNoBanco.Should().NotBeNull();
        }

        [Fact]
        public async Task Delete_OSComFaturamento_DeveRetornarRedirectComErro()
        {
            // Arrange
            var cliente = new Cliente { Nome = "Teste Cliente", Email = "teste@teste.com", Telefone = "12345678" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var os = new OrdemServico
            {
                ClienteId = cliente.Id,
                Equipamento = "Notebook",
                ProblemaRelatado = "Tela Quebrada",
                Status = "Concluído"
            };

            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            // Adicionar faturamento
            var faturamento = new Faturamento
            {
                OrdemServicoId = os.Id,
                ValorTotal = 150.00m,
                DataVencimento = DateTime.UtcNow.AddDays(5)
            };
            _context.Faturamentos.Add(faturamento);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Delete(os.Id);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
            redirectResult.RouteValues.Should().ContainKey("erro");
            redirectResult.RouteValues["erro"].ToString().Should().Contain("faturamento");

            // Verifica que não foi deletado
            var osNoBanco = await _context.OrdensServico.FindAsync(os.Id);
            osNoBanco.Should().NotBeNull();
        }

        [Fact]
        public async Task Delete_OSSemDependencias_DeveDeletarERetornarRedirectIndex()
        {
            // Arrange
            var cliente = new Cliente { Nome = "Teste Cliente", Email = "teste@teste.com", Telefone = "12345678" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var os = new OrdemServico
            {
                ClienteId = cliente.Id,
                Equipamento = "Notebook",
                ProblemaRelatado = "Tela Quebrada",
                Status = "Recebido"
            };
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Delete(os.Id);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
            redirectResult.RouteValues.Should().BeNull(); // Sem erro

            // Verifica que foi deletado
            var osNoBanco = await _context.OrdensServico.FindAsync(os.Id);
            osNoBanco.Should().BeNull();
        }

        [Fact]
        public async Task Delete_OSInexistente_DeveRetornarRedirectIndex()
        {
            // Act
            var result = await _controller.Delete(999);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
            redirectResult.RouteValues.Should().BeNull(); // Sem erro
        }


        [Fact]
        public async Task Delete_DbExceptionThrown_ReturnsRedirectWithErro()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            // Seed normal context
            using (var seedContext = new AppDbContext(options))
            {
                var os = new OrdemServico { Id = 9999, Equipamento = "Teste DB Error", ProblemaRelatado = "Falha", ClienteId = 1, Status = "Recebido" };
                seedContext.OrdensServico.Add(os);
                await seedContext.SaveChangesAsync();
            }

            // Create context that will throw exception
            var exceptionContext = new TestExceptionDbContext(options);

            var _mockEquipamentoBackupService = new Mock<IEquipamentoBackupService>();
            var localController = new AdminController(
                exceptionContext,
                _mockEstoqueService.Object,
                _mockEnv.Object,
                _mockPdfGeneratorService.Object,
                _mockDashboardService.Object,
                _mockEquipamentoBackupService.Object,
                _mockLogger.Object,
                _mockScopeFactory.Object
            );

            // Act
            var result = await localController.Delete(9999);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Which;
            redirectResult.ActionName.Should().Be("Index");
            redirectResult.RouteValues.Should().NotBeNull();
            redirectResult.RouteValues["erro"].Should().Be("Não foi possível excluir a OS.");

            // Verify logger was called
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("DB_DELETE_ERROR")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);

            localController.Dispose();
            exceptionContext.Dispose();
        }

        private class TestExceptionDbContext : AppDbContext
        {
            public TestExceptionDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                throw new TestDbException("Simulated database error");
            }
        }

        private class TestDbException : System.Data.Common.DbException
        {
            public TestDbException(string message) : base(message) { }
        }

        public void Dispose()
        {
            // We might have disposed the context in the test above, so we handle it gracefully
            try
            {
                _context.Database.EnsureDeleted();
                _context.Dispose();
            }
            catch (Exception ex) { Console.WriteLine($"Error during test teardown: {ex.Message}"); }
            _controller.Dispose();
        }
    }
}
