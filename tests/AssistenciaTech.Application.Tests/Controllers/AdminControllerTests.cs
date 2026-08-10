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
using Microsoft.EntityFrameworkCore;
using Moq;
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

            _controller = new AdminController(
                _context,
                _mockEstoqueService.Object,
                _mockEnv.Object,
                _mockPdfGeneratorService.Object,
                _mockDashboardService.Object
            );
        }

        [Fact]
        public async Task ExportarCsv_DeveRetornarArquivoCsvComDadosDasOrdensDeServico()
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

            // Act
            var result = await _controller.ExportarCsv();

            // Assert
            var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
            fileResult.ContentType.Should().Be("text/csv");
            fileResult.FileDownloadName.Should().Be("OrdensDeServico.csv");

            var csvString = Encoding.UTF8.GetString(fileResult.FileContents);
            var linhas = csvString.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            linhas.Should().HaveCount(3); // Cabecalho + 2 OS
            linhas[0].Should().Be("Id,Cliente,Equipamento,Data Entrada,Status,Valor Orçamento");

            // Ordem decrescente por ID
            linhas[1].Should().Contain("2,\"Cliente Teste\",\"Notebook\",05/10/2023,Concluído,200");
            linhas[2].Should().Contain("1,\"Cliente Teste\",\"PC Gamer\",01/10/2023,Orçamento,140");
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
