using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Controllers;
using FluentAssertions;
using Xunit;

namespace AssistenciaTech.Controllers.Tests
{
    public class FaturamentosControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly FaturamentosController _controller;

        public FaturamentosControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _controller = new FaturamentosController(_context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithFaturamentos()
        {
            // Arrange
            var os = new OrdemServico
            {
                Id = 1,
                CustoPecas = 150.50m,
                CustoMaoDeObra = 200.00m,
                DescontoAplicado = 50.00m,
                ProblemaRelatado = "Tela quebrada",
                Equipamento = "Smartphone Test",
                ClienteId = 1,
                TecnicoId = 1,
                DataEntrada = DateTime.Now
            };

            _context.OrdensServico.Add(os);

            var faturamento = new Faturamento
            {
                Id = 1,
                OrdemServicoId = 1,
                ValorTotal = 100.00m,
                DataVencimento = DateTime.Now.AddDays(3),
                StatusPagamento = PagamentoStatus.Pendente,
                TxIdPix = "123",
                QrCodePayload = "123"
            };
            _context.Faturamentos.Add(faturamento);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<Faturamento>>().Subject;
            model.Should().ContainSingle();
        }

        [Fact]
        public async Task GerarDaOS_ShouldReturnNotFound_WhenOsDoesNotExist()
        {
            // Act
            var result = await _controller.GerarDaOS(999);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task MarcarPago_ShouldUpdateStatusAndRedirect_WhenFaturamentoExists()
        {
            // Arrange
            var faturamento = new Faturamento
            {
                Id = 1,
                OrdemServicoId = 1,
                ValorTotal = 100.00m,
                DataVencimento = DateTime.Now.AddDays(3),
                StatusPagamento = PagamentoStatus.Pendente,
                TxIdPix = "123",
                QrCodePayload = "123"
            };

            _context.Faturamentos.Add(faturamento);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.MarcarPago(faturamento.Id);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be(nameof(FaturamentosController.Index));

            var faturamentoAtualizado = await _context.Faturamentos.FindAsync(faturamento.Id);
            faturamentoAtualizado.Should().NotBeNull();
            faturamentoAtualizado!.StatusPagamento.Should().Be(PagamentoStatus.Pago_Total);
        }

        [Fact]
        public async Task MarcarPago_ShouldRedirect_WhenFaturamentoDoesNotExist()
        {
            // Act
            var result = await _controller.MarcarPago(999);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be(nameof(FaturamentosController.Index));
        }

        [Fact]
        public async Task WebhookPix_ShouldReturnOk()
        {
            // Act
            var result = await _controller.WebhookPix(new { txId = "123" });

            // Assert
            result.Should().BeOfType<OkResult>();
        }

        [Fact]
        public async Task GerarDaOS_ShouldCreateFaturamentoAndRedirect_WhenOsExists()
        {
            // Arrange
            var os = new OrdemServico
            {
                Id = 1,
                CustoPecas = 150.50m,
                CustoMaoDeObra = 200.00m,
                DescontoAplicado = 50.00m,
                ProblemaRelatado = "Tela quebrada",
                Equipamento = "Smartphone Test",
                ClienteId = 1,
                TecnicoId = 1,
                DataEntrada = DateTime.Now
            };

            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GerarDaOS(os.Id);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be(nameof(FaturamentosController.Index));

            var faturamento = await _context.Faturamentos.FirstOrDefaultAsync(f => f.OrdemServicoId == os.Id);
            faturamento.Should().NotBeNull();
            faturamento!.ValorTotal.Should().Be(300.50m); // 150.50 + 200.00 - 50.00
            faturamento.StatusPagamento.Should().Be(PagamentoStatus.Pendente);
            faturamento.DataVencimento.Should().BeCloseTo(DateTime.Now.AddDays(3), TimeSpan.FromSeconds(5));
            faturamento.TxIdPix.Should().NotBeNullOrEmpty();
            faturamento.QrCodePayload.Should().Contain("300.50"); // Verify total in QR code
        }
    }
}
