using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.DTOs;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using AssistenciaTech.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class AdminControllerTests
    {
        private readonly Mock<ILogger<AdminController>> _mockLogger;
        private readonly Mock<IOrdemServicoLifecycle> _mockLifecycle;
        private readonly Mock<IAdminDashboardService> _mockDashboardService;
        private readonly Mock<IPdfGeneratorService> _mockPdfGeneratorService;
        private readonly Mock<IEvidenciaStorage> _mockEvidenciaStorage;
        private readonly AdminController _controller;

        public AdminControllerTests()
        {
            _mockLogger = new Mock<ILogger<AdminController>>();
            _mockLifecycle = new Mock<IOrdemServicoLifecycle>();
            _mockDashboardService = new Mock<IAdminDashboardService>();
            _mockPdfGeneratorService = new Mock<IPdfGeneratorService>();
            _mockEvidenciaStorage = new Mock<IEvidenciaStorage>();

            _controller = CreateController();
        }

        private AdminController CreateController()
        {
            var httpContext = new DefaultHttpContext();
            return new AdminController(
                _mockLogger.Object,
                _mockLifecycle.Object,
                _mockDashboardService.Object,
                _mockPdfGeneratorService.Object,
                _mockEvidenciaStorage.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };
        }

        [Fact]
        public async Task Index_Get_ReturnsViewWithDashboardData_WhenSuccessful()
        {
            var expectedData = new DashboardDto
            {
                TotalAbertas = 5,
                EquipamentosProntos = 2,
                FaturamentoPrevisto = 1500.50m,
                CurrentPage = 2,
                TotalPages = 3,
                TotalOrdens = 25,
                Ordens = new List<OrdemServico> { new() { Id = 1, Equipamento = "PC Gamer" } }
            };

            _mockDashboardService
                .Setup(s => s.GetDashboardDataAsync("test", "Aberto", 2, It.IsAny<int>()))
                .ReturnsAsync(expectedData);

            var result = await _controller.Index("test", "Aberto", 2);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<AdminDashboardViewModel>().Subject;
            model.SearchString.Should().Be("test");
            model.StatusFilter.Should().Be("Aberto");
            model.DashboardData.Should().BeSameAs(expectedData);
        }

        [Fact]
        public async Task Index_Get_ReturnsEmptyList_WhenDatabaseFails()
        {
            _mockDashboardService
                .Setup(s => s.GetDashboardDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ThrowsAsync(new SimulatedDbException("Simulated DB connection error"));

            var result = await _controller.Index(null, null, 1);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<AdminDashboardViewModel>().Subject;
            model.DashboardData.Ordens.Should().BeEmpty();
            model.ErroBanco.Should().Be("Erro ao conectar ao banco de dados. Por favor, tente novamente mais tarde.");
        }

        [Fact]
        public async Task ExportarCsv_ConfiguraHeadersEDelegaParaLifecycle()
        {
            var responseStream = new MemoryStream();
            _controller.ControllerContext.HttpContext.Response.Body = responseStream;

            _mockLifecycle
                .Setup(l => l.ExportarOrdensCsvAsync(responseStream, It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>((destino, _) =>
                {
                    var bytes = Encoding.UTF8.GetBytes("Id,Cliente,Equipamento\n1,\"Maria\",\"PC\"\n");
                    destino.Write(bytes, 0, bytes.Length);
                })
                .Returns(Task.CompletedTask);

            var result = await _controller.ExportarCsv();

            result.Should().BeOfType<EmptyResult>();
            _controller.Response.ContentType.Should().Be("text/csv");
            _controller.Response.Headers["Content-Disposition"].ToString().Should().Be("attachment; filename=\"OrdensDeServico.csv\"");
            Encoding.UTF8.GetString(responseStream.ToArray()).Should().Contain("\"Maria\",\"PC\"");
        }

        [Fact]
        public async Task ExportarCsv_DbExceptionThrown_ReturnsRedirectWithErroBanco()
        {
            _mockLifecycle
                .Setup(l => l.ExportarOrdensCsvAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SimulatedDbException("DB failure"));

            var result = await _controller.ExportarCsv();

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("Index");
            _controller.TempData["ErroBanco"].Should().Be("Erro ao exportar os dados. O banco de dados está inacessível.");
        }

        [Fact]
        public async Task Create_Get_ReturnsViewResult_WithClientesSelectList()
        {
            _mockLifecycle
                .Setup(l => l.ObterClientesParaSelecaoAsync())
                .ReturnsAsync(new List<SelecaoItemDto> { new(1, "Cliente Teste - CPF: 123 - Tel: 999") });

            var result = await _controller.Create();

            result.Should().BeOfType<ViewResult>();
            ((object)_controller.ViewBag.Clientes).Should().BeOfType<SelectList>();
        }

        [Fact]
        public async Task Create_Get_ReturnsRedirectToIndex_WhenLifecycleThrows()
        {
            _mockLifecycle
                .Setup(l => l.ObterClientesParaSelecaoAsync())
                .ThrowsAsync(new InvalidOperationException("DB offline"));

            var result = await _controller.Create();

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("Index");
            _controller.TempData["ErroBanco"].Should().Be("Não foi possível carregar a tela de criação. O banco de dados está inacessível.");
        }

        [Fact]
        public async Task Create_Post_ReturnsRedirectToIndexAndSetsWarrantyAlert_WhenValid()
        {
            var dto = new OrdemServicoCreateDto { ClienteId = 1, Equipamento = "PC Teste", NumeroSerie = "SN123" };
            _mockLifecycle
                .Setup(l => l.RegistrarEntradaAsync(dto, It.IsAny<string?>()))
                .ReturnsAsync(OrdemServicoResult.Ok(new OrdemServico { Id = 10 }, "ATENÇÃO: retorno em garantia!"));

            var result = await _controller.Create(dto);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("Index");
            _controller.TempData["AlertaGarantia"]?.ToString().Should().Contain("garantia");
        }

        [Fact]
        public async Task Create_Post_ReturnsViewWithClientesAndErrors_WhenLifecycleFailsOrThrows()
        {
            var dto = new OrdemServicoCreateDto { ClienteId = 1, Equipamento = "PC Teste" };
            _mockLifecycle
                .Setup(l => l.ObterClientesParaSelecaoAsync())
                .ReturnsAsync(new List<SelecaoItemDto>());
            _mockLifecycle
                .Setup(l => l.RegistrarEntradaAsync(dto, It.IsAny<string?>()))
                .ThrowsAsync(new InvalidOperationException("Outer", new Exception("Inner")));

            var result = await _controller.Create(dto);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeSameAs(dto);
            _controller.ModelState.Values.SelectMany(v => v.Errors)
                .Any(e => e.ErrorMessage.Contains("Outer | Inner: Inner"))
                .Should().BeTrue();
        }

        [Fact]
        public async Task Create_Post_ReturnsViewResult_WhenModelStateIsInvalid()
        {
            _controller.ModelState.AddModelError("Equipamento", "Required");
            _mockLifecycle
                .Setup(l => l.ObterClientesParaSelecaoAsync())
                .ReturnsAsync(new List<SelecaoItemDto> { new(1, "Cliente 1") });

            var dto = new OrdemServicoCreateDto { ClienteId = 1 };
            var result = await _controller.Create(dto);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeSameAs(dto);
            ((object)_controller.ViewBag.Clientes).Should().BeOfType<SelectList>();
            _mockLifecycle.Verify(l => l.RegistrarEntradaAsync(It.IsAny<OrdemServicoCreateDto>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Edit_Get_ReturnsNotFound_WhenIdIsNullOrOrdemMissing()
        {
            var nullResult = await _controller.Edit((int?)null);
            nullResult.Should().BeOfType<NotFoundResult>();

            _mockLifecycle.Setup(l => l.ObterDadosEdicaoAsync(999)).ReturnsAsync((OrdemEdicaoDto?)null);
            var missingResult = await _controller.Edit(999);
            missingResult.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Get_ReturnsViewWithOrdemAndLookups_WhenIdExists()
        {
            var dto = new OrdemEdicaoDto
            {
                Ordem = new OrdemServico { Id = 5, ClienteId = 1, TecnicoId = 2, EquipamentoBackupId = 3, ContratoId = 4 },
                Lookups = new OrdemEdicaoLookupsDto
                {
                    Tecnicos = new List<SelecaoItemDto> { new(2, "Carlos") },
                    EquipamentosBackup = new List<SelecaoItemDto> { new(3, "Notebook Reserva") },
                    Contratos = new List<SelecaoItemDto> { new(4, "Contrato: SLA 8h") }
                }
            };
            _mockLifecycle.Setup(l => l.ObterDadosEdicaoAsync(5)).ReturnsAsync(dto);

            var result = await _controller.Edit(5);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeSameAs(dto.Ordem);
            viewResult.ViewData["Tecnicos"].Should().BeOfType<SelectList>();
            viewResult.ViewData["EquipamentosBackup"].Should().BeOfType<SelectList>();
            viewResult.ViewData["Contratos"].Should().BeOfType<SelectList>();
        }

        [Fact]
        public async Task Edit_Get_ReturnsRedirectToIndex_WhenLifecycleThrows()
        {
            _mockLifecycle
                .Setup(l => l.ObterDadosEdicaoAsync(1))
                .ThrowsAsync(new InvalidOperationException("DB offline"));

            var result = await _controller.Edit(1);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("Index");
            _controller.TempData["ErroBanco"].Should().Be("Não foi possível carregar a tela de edição. O banco de dados está inacessível.");
        }

        [Fact]
        public async Task Edit_Post_ReturnsNotFound_WhenIdMismatchOrNaoEncontrada()
        {
            var mismatch = await _controller.Edit(1, new OrdemServico { Id = 2 }, null);
            mismatch.Should().BeOfType<NotFoundResult>();

            _mockLifecycle
                .Setup(l => l.AtualizarOrdemAsync(999, It.IsAny<OrdemServico>(), null, It.IsAny<string?>()))
                .ReturnsAsync(OrdemServicoResult.NaoEncontrado());

            var notFound = await _controller.Edit(999, new OrdemServico { Id = 999 }, null);
            notFound.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Post_ReturnsRedirectToIndex_WhenValid()
        {
            var os = new OrdemServico { Id = 10, Equipamento = "Dell" };
            _mockLifecycle
                .Setup(l => l.AtualizarOrdemAsync(10, os, null, It.IsAny<string?>()))
                .ReturnsAsync(OrdemServicoResult.Ok(os));

            var result = await _controller.Edit(10, os, null);

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("Index");
        }

        [Fact]
        public async Task Edit_Post_RepopulatesLookupsAndReturnsView_WhenValidationFailsOrExceptionOccurs()
        {
            var os = new OrdemServico { Id = 10, ClienteId = 1, EquipamentoBackupId = 2 };
            _mockLifecycle
                .Setup(l => l.AtualizarOrdemAsync(10, os, null, It.IsAny<string?>()))
                .ReturnsAsync(OrdemServicoResult.Falha("Backup não devolvido."));
            _mockLifecycle
                .Setup(l => l.ObterLookupsEdicaoAsync(1, 2))
                .ReturnsAsync(new OrdemEdicaoLookupsDto());

            var result = await _controller.Edit(10, os, null);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeSameAs(os);
            _controller.ModelState.ErrorCount.Should().Be(1);
            viewResult.ViewData["Tecnicos"].Should().BeOfType<SelectList>();
        }

        [Fact]
        public async Task Edit_Post_WhenLifecycleThrowsException_LogsErrorAndRepopulatesLookups()
        {
            var os = new OrdemServico { Id = 10, ClienteId = 1, EquipamentoBackupId = 2 };
            _mockLifecycle
                .Setup(l => l.AtualizarOrdemAsync(10, os, null, It.IsAny<string?>()))
                .ThrowsAsync(new InvalidOperationException("DB update failed"));
            _mockLifecycle
                .Setup(l => l.ObterLookupsEdicaoAsync(1, 2))
                .ReturnsAsync(new OrdemEdicaoLookupsDto());

            var result = await _controller.Edit(10, os, null);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeSameAs(os);
            _controller.ModelState.ErrorCount.Should().Be(1);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("DB_UPDATE_ERROR_EDIT")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Delete_MapsLifecycleResultAndExceptionsCorrectly()
        {
            _mockLifecycle.Setup(l => l.ExcluirOrdemAsync(1, It.IsAny<string?>()))
                .ReturnsAsync(OrdemServicoResult.Ok(new OrdemServico { Id = 1 }));
            var okResult = (await _controller.Delete(1)).Should().BeOfType<RedirectToActionResult>().Subject;
            okResult.RouteValues.Should().BeNull();

            _mockLifecycle.Setup(l => l.ExcluirOrdemAsync(999, It.IsAny<string?>()))
                .ReturnsAsync(OrdemServicoResult.NaoEncontrado());
            var notFoundResult = (await _controller.Delete(999)).Should().BeOfType<RedirectToActionResult>().Subject;
            notFoundResult.RouteValues.Should().BeNull();

            _mockLifecycle.Setup(l => l.ExcluirOrdemAsync(2, It.IsAny<string?>()))
                .ReturnsAsync(OrdemServicoResult.Falha("Possui faturamento."));
            var blockedResult = (await _controller.Delete(2)).Should().BeOfType<RedirectToActionResult>().Subject;
            blockedResult.RouteValues!["erro"].Should().Be("Possui faturamento.");

            _mockLifecycle.Setup(l => l.ExcluirOrdemAsync(3, It.IsAny<string?>()))
                .ThrowsAsync(new SimulatedDbException("FK violation"));
            var dbErrResult = (await _controller.Delete(3)).Should().BeOfType<RedirectToActionResult>().Subject;
            dbErrResult.RouteValues!["erro"].Should().Be("Não foi possível excluir a OS.");

            _mockLifecycle.Setup(l => l.ExcluirOrdemAsync(4, It.IsAny<string?>()))
                .ThrowsAsync(new InvalidOperationException("Unexpected"));
            var genErrResult = (await _controller.Delete(4)).Should().BeOfType<RedirectToActionResult>().Subject;
            genErrResult.RouteValues.Should().BeNull();
            _controller.TempData["ErrorMessage"].Should().Be("Ocorreu um erro ao tentar excluir a Ordem de Serviço.");
        }

        [Fact]
        public async Task ImprimirOs_ReturnsNotFoundOrPdfFile()
        {
            _mockLifecycle.Setup(l => l.ObterOrdemParaImpressaoAsync(999)).ReturnsAsync((OrdemServico?)null);
            (await _controller.ImprimirOs(999)).Should().BeOfType<NotFoundResult>();

            var os = new OrdemServico { Id = 12, Cliente = new Cliente { Nome = "Ana" } };
            var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
            _mockLifecycle.Setup(l => l.ObterOrdemParaImpressaoAsync(12)).ReturnsAsync(os);
            _mockPdfGeneratorService.Setup(p => p.GenerateOsPdf(os)).Returns(pdfBytes);

            var fileResult = (await _controller.ImprimirOs(12)).Should().BeOfType<FileContentResult>().Subject;
            fileResult.ContentType.Should().Be("application/pdf");
            fileResult.FileDownloadName.Should().Be("OS_12_Ana.pdf");
            fileResult.FileContents.Should().Equal(pdfBytes);
        }

        [Fact]
        public async Task GetEvidencia_MapeiaResultadosDoStorageEDeveExporRotaCanonicaELegada()
        {
            var routeTemplates = typeof(AdminController)
                .GetMethod(nameof(AdminController.GetEvidencia))!
                .GetCustomAttributes<HttpGetAttribute>()
                .Select(a => a.Template)
                .ToList();

            routeTemplates.Should().Contain("/Admin/GetEvidencia");
            routeTemplates.Should().Contain("/Admin/DownloadEvidencia/{fileName}");

            _mockEvidenciaStorage.Setup(s => s.AbrirLeituraAsync("../x.jpg"))
                .ReturnsAsync(EvidenciaReadResult.Invalido());
            (await _controller.GetEvidencia("../x.jpg")).Should().BeOfType<BadRequestResult>();

            _mockEvidenciaStorage.Setup(s => s.AbrirLeituraAsync("missing.jpg"))
                .ReturnsAsync(EvidenciaReadResult.NaoEncontrado());
            (await _controller.GetEvidencia("missing.jpg")).Should().BeOfType<NotFoundResult>();

            await using var ms = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
            _mockEvidenciaStorage.Setup(s => s.AbrirLeituraAsync("foto.jpg"))
                .ReturnsAsync(EvidenciaReadResult.Ok(ms, "image/jpeg"));

            var streamResult = (await _controller.GetEvidencia("foto.jpg")).Should().BeOfType<FileStreamResult>().Subject;
            streamResult.ContentType.Should().Be("image/jpeg");
            streamResult.FileStream.Should().BeSameAs(ms);
        }

        private sealed class SimulatedDbException : DbException
        {
            public SimulatedDbException(string message) : base(message) { }
        }
    }
}
