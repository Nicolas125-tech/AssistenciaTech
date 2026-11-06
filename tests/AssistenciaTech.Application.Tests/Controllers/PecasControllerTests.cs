using System.Collections.Generic;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.DTOs;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class PecasControllerTests
    {
        private readonly Mock<IEstoqueEngine> _mockEngine;
        private readonly PecasController _controller;

        public PecasControllerTests()
        {
            _mockEngine = new Mock<IEstoqueEngine>();
            _controller = new PecasController(_mockEngine.Object);

            var httpContext = new DefaultHttpContext();
            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            _controller.TempData = tempData;
        }

        [Fact]
        public async Task Index_DeveRetornarViewComListaDePecas()
        {
            // Arrange
            var pecas = new List<Peca>
            {
                new Peca { Id = 1, Nome = "Peça 1" },
                new Peca { Id = 2, Nome = "Peça 2" }
            };
            _mockEngine.Setup(e => e.ObterTodasAsync()).ReturnsAsync(pecas);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeEquivalentTo(pecas);
        }

        [Fact]
        public void Create_GET_DeveRetornarView()
        {
            // Act
            var result = _controller.Create();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public async Task Create_POST_ComDadosValidosESucessoNoEngine_DeveRedirecionarParaIndex()
        {
            // Arrange
            var dto = new PecaCreateDto { Nome = "Nova Peça", QuantidadeEstoque = 10, QuantidadeMinima = 2, ValorUnitario = 50 };
            _mockEngine.Setup(e => e.CriarPecaAsync(dto))
                .ReturnsAsync(EstoqueResult.Ok(new Peca { Id = 1, Nome = dto.Nome }, "Peça cadastrada com sucesso."));

            // Act
            var result = await _controller.Create(dto);

            // Assert
            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be(nameof(PecasController.Index));
            _controller.TempData["MensagemSucesso"].Should().Be("Peça cadastrada com sucesso.");
        }

        [Fact]
        public async Task Create_POST_ComFalhaNoEngine_DeveAdicionarErroNoModelStateERetornarView()
        {
            // Arrange
            var dto = new PecaCreateDto { Nome = "Nova Peça" };
            _mockEngine.Setup(e => e.CriarPecaAsync(dto))
                .ReturnsAsync(EstoqueResult.Falha("Erro ao cadastrar peça."));

            // Act
            var result = await _controller.Create(dto);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().Be(dto);
            _controller.ModelState.IsValid.Should().BeFalse();
        }

        [Fact]
        public async Task Edit_GET_ComIdNulo_DeveRetornarNotFound()
        {
            // Act
            var result = await _controller.Edit(null);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_GET_ComIdInexistente_DeveRetornarNotFound()
        {
            // Arrange
            _mockEngine.Setup(e => e.ObterPorIdAsync(999)).ReturnsAsync((Peca?)null);

            // Act
            var result = await _controller.Edit(999);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_GET_ComIdExistente_DeveRetornarViewComDto()
        {
            // Arrange
            var peca = new Peca { Id = 1, Nome = "Peça 1", QuantidadeEstoque = 5, QuantidadeMinima = 1, ValorUnitario = 100 };
            _mockEngine.Setup(e => e.ObterPorIdAsync(1)).ReturnsAsync(peca);

            // Act
            var result = await _controller.Edit(1);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var dto = viewResult.Model.Should().BeOfType<PecaEditDto>().Subject;
            dto.Id.Should().Be(1);
            dto.Nome.Should().Be("Peça 1");
        }

        [Fact]
        public async Task Edit_POST_ComIdIncompativel_DeveRetornarNotFound()
        {
            // Arrange
            var dto = new PecaEditDto { Id = 2 };

            // Act
            var result = await _controller.Edit(1, dto);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_POST_ComSucessoNoEngine_DeveRedirecionarParaIndex()
        {
            // Arrange
            var dto = new PecaEditDto { Id = 1, Nome = "Peça Atualizada" };
            _mockEngine.Setup(e => e.AtualizarPecaAsync(dto))
                .ReturnsAsync(EstoqueResult.Ok(null, "Peça atualizada com sucesso."));

            // Act
            var result = await _controller.Edit(1, dto);

            // Assert
            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be(nameof(PecasController.Index));
            _controller.TempData["MensagemSucesso"].Should().Be("Peça atualizada com sucesso.");
        }

        [Fact]
        public async Task Edit_POST_QuandoNaoEncontradaNoEngine_DeveRetornarNotFound()
        {
            // Arrange
            var dto = new PecaEditDto { Id = 1, Nome = "Peça" };
            _mockEngine.Setup(e => e.AtualizarPecaAsync(dto))
                .ReturnsAsync(EstoqueResult.Falha("Peça não encontrada."));

            // Act
            var result = await _controller.Edit(1, dto);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task DeleteConfirmed_POST_ComSucesso_DeveDefinirMensagemSucessoERedirecionar()
        {
            // Arrange
            _mockEngine.Setup(e => e.ExcluirPecaAsync(1))
                .ReturnsAsync(EstoqueResult.Ok(null, "Peça excluída com sucesso."));

            // Act
            var result = await _controller.DeleteConfirmed(1);

            // Assert
            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be(nameof(PecasController.Index));
            _controller.TempData["MensagemSucesso"].Should().Be("Peça excluída com sucesso.");
        }

        [Fact]
        public async Task DeleteConfirmed_POST_ComFalhaDeIntegridade_DeveDefinirMensagemErroERedirecionar()
        {
            // Arrange
            _mockEngine.Setup(e => e.ExcluirPecaAsync(1))
                .ReturnsAsync(EstoqueResult.Falha("Não é possível excluir a peça vinculada a ordens de serviço."));

            // Act
            var result = await _controller.DeleteConfirmed(1);

            // Assert
            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be(nameof(PecasController.Index));
            _controller.TempData["MensagemErro"].Should().Be("Não é possível excluir a peça vinculada a ordens de serviço.");
        }
    }
}
