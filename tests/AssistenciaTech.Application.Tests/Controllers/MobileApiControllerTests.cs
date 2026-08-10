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
                CheckIn = DateTime.Now
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
                CheckIn = DateTime.Now
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

            // Check properties using reflection/dynamics or just assert it's an OkObjectResult
            var osInDb = await _context.OrdensServico.FindAsync(1);
            osInDb!.Status.Should().Be(WorkflowStatus.Concluido);

            var visitaInDb = await _context.VisitasCampo.FindAsync(1);
            visitaInDb!.CheckOut.Should().NotBeNull();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
