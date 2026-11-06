using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.DTOs;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class MobileApiControllerTests
    {
        private readonly Mock<IVisitaCampoEngine> _mockEngine;
        private readonly MobileApiController _controller;

        public MobileApiControllerTests()
        {
            _mockEngine = new Mock<IVisitaCampoEngine>();
            _controller = new MobileApiController(_mockEngine.Object);
        }

        private void SetUserContext(int tecnicoId, string nome = "Tecnico")
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, tecnicoId.ToString()),
                new Claim(ClaimTypes.Name, nome)
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [Fact]
        public async Task Login_ComCredenciaisValidas_DeveRetornarOkComToken()
        {
            // Arrange
            var request = new MobileApiController.MobileLoginRequest
            {
                Username = "carlos",
                Password = "123"
            };

            _mockEngine.Setup(e => e.AutenticarTecnicoAsync("carlos", "123"))
                .ReturnsAsync(MobileAuthResult.Ok("mock-jwt-token", 5, "carlos"));

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task Login_ComCredenciaisInvalidas_DeveRetornarUnauthorized()
        {
            // Arrange
            var request = new MobileApiController.MobileLoginRequest
            {
                Username = "carlos",
                Password = "errada"
            };

            _mockEngine.Setup(e => e.AutenticarTecnicoAsync("carlos", "errada"))
                .ReturnsAsync(MobileAuthResult.Falha("Usuário ou senha inválidos."));

            // Act
            var result = await _controller.Login(request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task CheckIn_SemAutenticacao_DeveRetornarUnauthorized()
        {
            // Arrange - Sem Claims
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var request = new MobileApiController.CheckInRequest { Latitude = -23, Longitude = -46 };

            // Act
            var result = await _controller.CheckIn(1, request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task CheckIn_QuandoOsNaoEncontrada_DeveRetornarNotFound()
        {
            // Arrange
            SetUserContext(10);
            var request = new MobileApiController.CheckInRequest { Latitude = -23, Longitude = -46 };

            _mockEngine.Setup(e => e.RegistrarCheckInAsync(999, 10, -23, -46))
                .ReturnsAsync(VisitaResult.NaoEncontrado("OS não encontrada."));

            // Act
            var result = await _controller.CheckIn(999, request);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task CheckIn_QuandoTecnicoNaoAutorizado_DeveRetornarForbid()
        {
            // Arrange
            SetUserContext(10);
            var request = new MobileApiController.CheckInRequest { Latitude = -23, Longitude = -46 };

            _mockEngine.Setup(e => e.RegistrarCheckInAsync(100, 10, -23, -46))
                .ReturnsAsync(VisitaResult.Proibido());

            // Act
            var result = await _controller.CheckIn(100, request);

            // Assert
            result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task CheckIn_ComSucesso_DeveRetornarOk()
        {
            // Arrange
            SetUserContext(10);
            var request = new MobileApiController.CheckInRequest { Latitude = -23, Longitude = -46 };

            _mockEngine.Setup(e => e.RegistrarCheckInAsync(100, 10, -23, -46))
                .ReturnsAsync(VisitaResult.Ok("Check-in realizado com sucesso", 42));

            // Act
            var result = await _controller.CheckIn(100, request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task FinalizarVisita_SemAutenticacao_DeveRetornarUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var request = new MobileApiController.FinalizarRequest { VisitaId = 1 };

            // Act
            var result = await _controller.FinalizarVisita(1, request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task FinalizarVisita_QuandoVisitaNaoEncontrada_DeveRetornarNotFound()
        {
            // Arrange
            SetUserContext(10, "Carlos");
            var request = new MobileApiController.FinalizarRequest { VisitaId = 99 };

            _mockEngine.Setup(e => e.FinalizarVisitaAsync(1, 99, 10, It.IsAny<string>(), It.IsAny<string>(), "Carlos"))
                .ReturnsAsync(VisitaResult.NaoEncontrado("Visita não encontrada ou não pertence a esta OS"));

            // Act
            var result = await _controller.FinalizarVisita(1, request);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task FinalizarVisita_QuandoTecnicoNaoAutorizado_DeveRetornarForbid()
        {
            // Arrange
            SetUserContext(10, "Carlos");
            var request = new MobileApiController.FinalizarRequest { VisitaId = 1 };

            _mockEngine.Setup(e => e.FinalizarVisitaAsync(1, 1, 10, It.IsAny<string>(), It.IsAny<string>(), "Carlos"))
                .ReturnsAsync(VisitaResult.Proibido());

            // Act
            var result = await _controller.FinalizarVisita(1, request);

            // Assert
            result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task FinalizarVisita_QuandoJaFinalizadaOuErroNoLifecycle_DeveRetornarBadRequest()
        {
            // Arrange
            SetUserContext(10, "Carlos");
            var request = new MobileApiController.FinalizarRequest { VisitaId = 1 };

            _mockEngine.Setup(e => e.FinalizarVisitaAsync(1, 1, 10, It.IsAny<string>(), It.IsAny<string>(), "Carlos"))
                .ReturnsAsync(VisitaResult.Invalido("Visita já finalizada."));

            // Act
            var result = await _controller.FinalizarVisita(1, request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task FinalizarVisita_ComSucesso_DeveRetornarOk()
        {
            // Arrange
            SetUserContext(10, "Carlos");
            var request = new MobileApiController.FinalizarRequest
            {
                VisitaId = 1,
                AssinaturaBase64 = "base64",
                LaudoFinal = "Reparo concluído"
            };

            _mockEngine.Setup(e => e.FinalizarVisitaAsync(1, 1, 10, "base64", "Reparo concluído", "Carlos"))
                .ReturnsAsync(VisitaResult.Ok("Visita finalizada e OS atualizada."));

            // Act
            var result = await _controller.FinalizarVisita(1, request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task SyncVisitas_SemAutenticacao_DeveRetornarUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await _controller.SyncVisitas(new List<VisitaCampoSyncDto>());

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task SyncVisitas_QuandoEngineRetornarFalha_DeveRetornarBadRequest()
        {
            // Arrange
            SetUserContext(10);
            var dtos = new List<VisitaCampoSyncDto>();

            _mockEngine.Setup(e => e.SincronizarVisitasOfflineAsync(10, dtos))
                .ReturnsAsync(VisitaSyncResult.Falha("Nenhum dado para sincronizar."));

            // Act
            var result = await _controller.SyncVisitas(dtos);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task SyncVisitas_ComSucesso_DeveRetornarOk()
        {
            // Arrange
            SetUserContext(10);
            var dtos = new List<VisitaCampoSyncDto>
            {
                new VisitaCampoSyncDto { OrdemServicoId = 1, CheckIn = DateTime.UtcNow }
            };

            _mockEngine.Setup(e => e.SincronizarVisitasOfflineAsync(10, dtos))
                .ReturnsAsync(VisitaSyncResult.Ok("1 visitas sincronizadas com sucesso.", 1, 1, 0));

            // Act
            var result = await _controller.SyncVisitas(dtos);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }
    }
}
