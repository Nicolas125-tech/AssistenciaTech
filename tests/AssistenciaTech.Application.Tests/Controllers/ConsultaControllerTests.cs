using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class ConsultaControllerTests
    {
        private readonly Mock<IAreaClienteEngine> _areaClienteEngineMock;
        private readonly Mock<ILogger<ConsultaController>> _loggerMock;
        private readonly ConsultaController _controller;

        public ConsultaControllerTests()
        {
            _areaClienteEngineMock = new Mock<IAreaClienteEngine>();
            _loggerMock = new Mock<ILogger<ConsultaController>>();
            _controller = new ConsultaController(_areaClienteEngineMock.Object, _loggerMock.Object);
        }

        [Fact]
        public void Index_DeveRetornarViewResult()
        {
            // Act
            var result = _controller.Index(null);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Which;
            viewResult.ViewName.Should().BeNull();
        }

        [Fact]
        public void Index_ComNumeroOS_DeveDefinirViewBag()
        {
            // Act
            var result = _controller.Index(42);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Which;
            viewResult.ViewName.Should().BeNull();
            ((int)_controller.ViewBag.NumeroOS).Should().Be(42);
        }

        [Fact]
        public async Task Status_ComCpfMuitoLongo_DeveRetornarErroDeValidacao_PrevenindoDoS()
        {
            // Arrange
            int numeroOS = 1;
            string cpfLongo = new string('9', 100);
            _areaClienteEngineMock.Setup(x => x.ConsultarStatusPublicoAsync(numeroOS, cpfLongo))
                .ReturnsAsync(ConsultaPublicaResult.Falha("Por favor, preencha o número da OS e o CPF."));

            // Act
            var result = await _controller.Status(numeroOS, cpfLongo);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Index");
            _controller.ModelState.IsValid.Should().BeFalse();
            var erro = _controller.ModelState[string.Empty]?.Errors.FirstOrDefault()?.ErrorMessage;
            erro.Should().Be("Por favor, preencha o número da OS e o CPF.");
        }

        [Fact]
        public async Task Status_ComOrdemInexistente_DeveRetornarErro()
        {
            // Arrange
            int numeroOS = 999;
            string cpf = "12345678901";
            _areaClienteEngineMock.Setup(x => x.ConsultarStatusPublicoAsync(numeroOS, cpf))
                .ReturnsAsync(ConsultaPublicaResult.Falha("Ordem de Serviço não encontrada ou CPF inválido."));

            // Act
            var result = await _controller.Status(numeroOS, cpf);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Index");
            _controller.ModelState.IsValid.Should().BeFalse();
            var erro = _controller.ModelState[string.Empty]?.Errors.FirstOrDefault()?.ErrorMessage;
            erro.Should().Be("Ordem de Serviço não encontrada ou CPF inválido.");
        }

        [Fact]
        public async Task Status_ComNumeroOSInvalido_DeveRetornarErroDeValidacao()
        {
            // Arrange
            int numeroOS = 0;
            string cpf = "12345678901";
            _areaClienteEngineMock.Setup(x => x.ConsultarStatusPublicoAsync(numeroOS, cpf))
                .ReturnsAsync(ConsultaPublicaResult.Falha("Por favor, preencha o número da OS e o CPF."));

            // Act
            var result = await _controller.Status(numeroOS, cpf);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Index");
            _controller.ModelState.IsValid.Should().BeFalse();
            var erro = _controller.ModelState[string.Empty]?.Errors.FirstOrDefault()?.ErrorMessage;
            erro.Should().Be("Por favor, preencha o número da OS e o CPF.");
        }

        [Fact]
        public async Task Status_ComDadosValidos_DeveRetornarViewDetalhesComOrdem()
        {
            // Arrange
            var os = new OrdemServico { Id = 10, Equipamento = "MacBook Pro", Status = "Em Andamento" };
            _areaClienteEngineMock.Setup(x => x.ConsultarStatusPublicoAsync(10, "12345678901"))
                .ReturnsAsync(ConsultaPublicaResult.Ok(os));

            // Act
            var result = await _controller.Status(10, "12345678901");

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Detalhes");
            viewResult.Model.Should().Be(os);
        }

        [Fact]
        public async Task Status_QuandoExcecaoLancada_DeveRetornarViewIndexComErro()
        {
            // Arrange
            _areaClienteEngineMock.Setup(x => x.ConsultarStatusPublicoAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Erro de conexão simulado"));

            // Act
            var result = await _controller.Status(1, "12345678901");

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Index");
            _controller.ModelState.IsValid.Should().BeFalse();
            var erro = _controller.ModelState[string.Empty]?.Errors.FirstOrDefault()?.ErrorMessage;
            erro.Should().Be("Ocorreu um erro ao processar sua solicitação. Por favor, tente novamente mais tarde.");
        }

        [Fact]
        public async Task MeusEquipamentos_SemEmail_DeveRedirecionarParaIndex()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            // Act
            var result = await _controller.MeusEquipamentos();

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
        }

        [Fact]
        public async Task MeusEquipamentos_ComEmail_DeveRetornarViewComOrdensDoCliente()
        {
            // Arrange
            var userEmail = "cliente@teste.com";
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Email, userEmail)
                    }))
                }
            };

            var os1 = new OrdemServico { Id = 2, Equipamento = "Note", DataEntrada = DateTime.UtcNow };
            var os2 = new OrdemServico { Id = 1, Equipamento = "PC", DataEntrada = DateTime.UtcNow.AddDays(-1) };
            var lista = new List<OrdemServico> { os1, os2 };

            _areaClienteEngineMock.Setup(x => x.ObterEquipamentosPorEmailAsync(userEmail))
                .ReturnsAsync(lista);

            // Act
            var result = await _controller.MeusEquipamentos();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var ordens = viewResult.Model.Should().BeAssignableTo<IEnumerable<OrdemServico>>().Subject.ToList();

            ordens.Should().HaveCount(2);
            ordens[0].Id.Should().Be(2);
            ordens[1].Id.Should().Be(1);
        }

        [Fact]
        public async Task VisualizarOS_SemEmail_DeveRedirecionarParaIndex()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            // Act
            var result = await _controller.VisualizarOS(1);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
        }

        [Fact]
        public async Task VisualizarOS_OrdemInexistente_DeveRetornarNotFound()
        {
            // Arrange
            var userEmail = "cliente@teste.com";
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Email, userEmail)
                    }))
                }
            };

            _areaClienteEngineMock.Setup(x => x.ObterDetalhesSegurosAsync(999, userEmail))
                .ReturnsAsync(VisualizacaoOSResult.NaoEncontrado());

            // Act
            var result = await _controller.VisualizarOS(999);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().Be("Ordem de Serviço não encontrada ou acesso não autorizado.");
        }

        [Fact]
        public async Task VisualizarOS_OrdemDeOutroCliente_DeveRetornarNotFound()
        {
            // Arrange
            var userEmail = "cliente@teste.com";
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Email, userEmail)
                    }))
                }
            };

            _areaClienteEngineMock.Setup(x => x.ObterDetalhesSegurosAsync(50, userEmail))
                .ReturnsAsync(VisualizacaoOSResult.NaoEncontrado());

            // Act
            var result = await _controller.VisualizarOS(50);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().Be("Ordem de Serviço não encontrada ou acesso não autorizado.");
        }

        [Fact]
        public async Task VisualizarOS_OrdemPertenceAoCliente_DeveRetornarViewDetalhes()
        {
            // Arrange
            var userEmail = "cliente@teste.com";
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Email, userEmail)
                    }))
                }
            };

            var os = new OrdemServico { Id = 1, Equipamento = "PC" };
            _areaClienteEngineMock.Setup(x => x.ObterDetalhesSegurosAsync(1, userEmail))
                .ReturnsAsync(VisualizacaoOSResult.Ok(os));

            // Act
            var result = await _controller.VisualizarOS(1);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Detalhes");
            var ordemRetornada = viewResult.Model.Should().BeAssignableTo<OrdemServico>().Subject;
            ordemRetornada.Id.Should().Be(1);
        }

        [Fact]
        public async Task VisualizarOS_QuandoExcecaoLancada_DeveRetornarViewIndexComErro()
        {
            // Arrange
            var userEmail = "cliente@teste.com";
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Email, userEmail)
                    }))
                }
            };

            _areaClienteEngineMock.Setup(x => x.ObterDetalhesSegurosAsync(1, userEmail))
                .ThrowsAsync(new InvalidOperationException("Falha simulada"));

            // Act
            var result = await _controller.VisualizarOS(1);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Index");
            _controller.ModelState.IsValid.Should().BeFalse();
            var erro = _controller.ModelState[string.Empty]?.Errors.FirstOrDefault()?.ErrorMessage;
            erro.Should().Be("Ocorreu um erro ao processar sua solicitação. Por favor, tente novamente mais tarde.");
        }
    }
}
