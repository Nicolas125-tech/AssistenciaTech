using System;
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
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class AdminControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IEstoqueService> _mockEstoqueService;
        private readonly Mock<IWebHostEnvironment> _mockEnv;
        private readonly Mock<IPdfGeneratorService> _mockPdfGeneratorService;
        private readonly Mock<IAdminDashboardService> _mockDashboardService;
        private readonly Mock<ILogger<AdminController>> _mockLogger;
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

            var _mockEquipamentoBackupService = new Mock<IEquipamentoBackupService>();
            _controller = new AdminController(
                _context,
                _mockEstoqueService.Object,
                _mockEnv.Object,
                _mockPdfGeneratorService.Object,
                _mockDashboardService.Object,
                _mockEquipamentoBackupService.Object,
                _mockLogger.Object
            );
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
                DataEntrada = DateTime.Now.AddDays(-10), // Menos de 30 dias
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

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
