using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class MobileApiControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly MobileApiController _controller;

        public MobileApiControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _controller = new MobileApiController(_context);
        }

        private void SetUserContext(int tecnicoId)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, tecnicoId.ToString())
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }


        [Fact]
        public async Task FinalizarVisita_ReturnsNotFound_WhenVisitaIsNotFound()
        {
            // Arrange
            // Empty database, no VisitaCampo exists

            SetUserContext(10); // Any technician ID

            var request = new MobileApiController.FinalizarRequest { VisitaId = 999 }; // Non-existent VisitaId

            // Act
            var result = await _controller.FinalizarVisita(1, request); // Any OS ID

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().BeEquivalentTo(new { error = "Visita não encontrada ou não pertence a esta OS" });
        }

        [Fact]
        public async Task FinalizarVisita_ReturnsNotFound_WhenOrdemServicoIsNotFound()
        {
            // Arrange
            var visita = new VisitaCampo
            {
                Id = 1,
                OrdemServicoId = 99, // Represents a non-existent OS
                TecnicoId = 10,
                CheckIn = DateTime.UtcNow
            };
            _context.VisitasCampo.Add(visita);
            await _context.SaveChangesAsync();

            SetUserContext(10);

            var request = new MobileApiController.FinalizarRequest { VisitaId = 1 };

            // Act
            var result = await _controller.FinalizarVisita(99, request);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task FinalizarVisita_ReturnsForbid_WhenVisitaTecnicoIdDoesNotMatchAuthenticatedUser()
        {
            // Arrange
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.EmAnalise };
            _context.OrdensServico.Add(os);

            var visita = new VisitaCampo
            {
                Id = 1,
                OrdemServicoId = 1,
                TecnicoId = 10, // A different technician
                CheckIn = DateTime.UtcNow
            };
            _context.VisitasCampo.Add(visita);
            await _context.SaveChangesAsync();

            // Authenticate as a different technician (ID = 20)
            SetUserContext(20);

            var request = new MobileApiController.FinalizarRequest { VisitaId = 1 };

            // Act
            var result = await _controller.FinalizarVisita(1, request);

            // Assert
            result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task FinalizarVisita_ReturnsOk_WhenVisitaTecnicoIdMatchesAuthenticatedUser()
        {
            // Arrange
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.EmAnalise };
            _context.OrdensServico.Add(os);

            var visita = new VisitaCampo
            {
                Id = 1,
                OrdemServicoId = 1,
                TecnicoId = 10, // The authenticated technician
                CheckIn = DateTime.UtcNow
            };
            _context.VisitasCampo.Add(visita);
            await _context.SaveChangesAsync();

            // Authenticate as the correct technician (ID = 10)
            SetUserContext(10);

            var request = new MobileApiController.FinalizarRequest { VisitaId = 1 };

            // Act
            var result = await _controller.FinalizarVisita(1, request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;

            var resultObj = okResult.Value;
            resultObj.Should().NotBeNull();

            var osInDb = await _context.OrdensServico.FindAsync(1);
            osInDb!.Status.Should().Be(WorkflowStatus.Concluido);

            var visitaInDb = await _context.VisitasCampo.FindAsync(1);
            visitaInDb!.CheckOut.Should().NotBeNull();
        }

        [Fact]
        public async Task FinalizarVisita_ReturnsBadRequest_WhenVisitaAlreadyFinalized()
        {
            // Arrange
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.EmAnalise };
            _context.OrdensServico.Add(os);

            var visita = new VisitaCampo
            {
                Id = 1,
                OrdemServicoId = 1,
                TecnicoId = 10, // The authenticated technician
                CheckIn = DateTime.UtcNow.AddHours(-1),
                CheckOut = DateTime.UtcNow // Already finalized
            };
            _context.VisitasCampo.Add(visita);
            await _context.SaveChangesAsync();

            // Authenticate as the correct technician (ID = 10)
            SetUserContext(10);

            var request = new MobileApiController.FinalizarRequest { VisitaId = 1 };

            // Act
            var result = await _controller.FinalizarVisita(1, request);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { error = "Visita já finalizada" });
        }

        [Fact]
        public async Task CheckIn_ReturnsUnauthorized_WhenUserIsNotAuthenticated()
        {
            // Arrange
            // Do not set the user context, so User.FindFirst(ClaimTypes.NameIdentifier) returns null
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext() // User is unauthenticated
            };

            var request = new MobileApiController.CheckInRequest { Latitude = 10m, Longitude = 20m };

            // Act
            var result = await _controller.CheckIn(1, request);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().BeEquivalentTo(new { error = "Técnico não autenticado." });
        }

        [Fact]
        public async Task FinalizarVisita_ReturnsUnauthorized_WhenUserIsNotAuthenticated()
        {
            // Arrange
            // Do not set the user context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext() // User is unauthenticated
            };

            var request = new MobileApiController.FinalizarRequest { VisitaId = 1 };

            // Act
            var result = await _controller.FinalizarVisita(1, request);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().BeEquivalentTo(new { error = "Técnico não autenticado." });
        }

        [Fact]
        public async Task CheckIn_ReturnsForbid_WhenOrdemServicoTecnicoIdDoesNotMatchAuthenticatedUser()
        {
            // Arrange
            var os = new OrdemServico { Id = 2, Equipamento = "Notebook", Status = WorkflowStatus.Recebido, TecnicoId = 10 };
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            // Authenticate as a different technician (ID = 20)
            SetUserContext(20);

            var request = new MobileApiController.CheckInRequest { Latitude = 10m, Longitude = 20m };

            // Act
            var result = await _controller.CheckIn(2, request);

            // Assert
            result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task CheckIn_ReturnsOk_WhenOrdemServicoTecnicoIdMatchesAuthenticatedUser()
        {
            // Arrange
            var os = new OrdemServico { Id = 2, Equipamento = "Notebook", Status = WorkflowStatus.Recebido, TecnicoId = 10 };
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            // Authenticate as the correct technician (ID = 10)
            SetUserContext(10);

            var request = new MobileApiController.CheckInRequest { Latitude = 10m, Longitude = 20m };

            // Act
            var result = await _controller.CheckIn(2, request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();

            var visita = await _context.VisitasCampo.FirstOrDefaultAsync(v => v.OrdemServicoId == 2);
            visita.Should().NotBeNull();
            visita!.TecnicoId.Should().Be(10);
            visita.Latitude.Should().Be(10m);
            visita.Longitude.Should().Be(20m);
        }

        [Fact]
        public async Task CheckIn_ReturnsNotFound_WhenOrdemServicoDoesNotExist()
        {
            // Arrange
            SetUserContext(10);
            var request = new MobileApiController.CheckInRequest { Latitude = 10m, Longitude = 20m };

            // Act
            var result = await _controller.CheckIn(999, request);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().BeEquivalentTo(new { error = "OS não encontrada" });
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
