using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class FaturamentosControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly FaturamentosController _controller;

        public FaturamentosControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _mockConfiguration = new Mock<IConfiguration>();
            _controller = new FaturamentosController(_context, _mockConfiguration.Object);
        }

        [Fact]
        public async Task GerarDaOS_ReturnsNotFound_WhenOrdemServicoDoesNotExist()
        {
            // Act
            var result = await _controller.GerarDaOS(999);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task GerarDaOS_CreatesFaturamentoAndRedirectsToIndex_WhenOrdemServicoExists()
        {
            // Arrange
            var os = new OrdemServico
            {
                Id = 1,
                Equipamento = "PC Gamer",
                CustoPecas = 150m,
                CustoMaoDeObra = 100m,
                DescontoAplicado = 20m,
                Status = WorkflowStatus.Concluido
            };
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GerarDaOS(1);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");

            var faturamento = await _context.Faturamentos.FirstOrDefaultAsync(f => f.OrdemServicoId == 1);
            faturamento.Should().NotBeNull();

            decimal expectedTotal = (150m + 100m) - 20m;
            faturamento!.ValorTotal.Should().Be(expectedTotal);
            faturamento.StatusPagamento.Should().Be(PagamentoStatus.Pendente);
            faturamento.DataVencimento.Should().BeCloseTo(DateTime.Now.AddDays(3), TimeSpan.FromMinutes(1));

            faturamento.TxIdPix.Should().NotBeNullOrEmpty();
            faturamento.QrCodePayload.Should().NotBeNullOrEmpty();
            faturamento.QrCodePayload.Should().Contain(expectedTotal.ToString("0.00").Replace(",", "."));
            faturamento.QrCodePayload.Should().Contain(faturamento.TxIdPix);
        }

        [Fact]
        public async Task GerarDaOS_HandlesZeroTotalOrdemServico()
        {
            // Arrange
            var os = new OrdemServico
            {
                Id = 1,
                Equipamento = "Free Inspection",
                CustoPecas = 0m,
                CustoMaoDeObra = 0m,
                DescontoAplicado = 0m,
                Status = WorkflowStatus.Concluido
            };
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GerarDaOS(1);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");

            var faturamento = await _context.Faturamentos.FirstOrDefaultAsync(f => f.OrdemServicoId == 1);
            faturamento.Should().NotBeNull();

            faturamento!.ValorTotal.Should().Be(0m);
            faturamento.QrCodePayload.Should().Contain("0.00");
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
