using System;
using System.Collections.Generic;
using System.Linq;
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
    public class MobileApiControllerSyncTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly MobileApiController _controller;

        public MobileApiControllerSyncTests()
        {
            var mockConfiguration = new Moq.Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _controller = new MobileApiController(_context, mockConfiguration.Object);
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
        public async Task SyncVisitas_ReturnsForbid_WhenTryingToSyncOtherTechniciansOS()
        {
            // Arrange
            var osOutroTecnico = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.Recebido, TecnicoId = 99 };
            _context.OrdensServico.Add(osOutroTecnico);
            await _context.SaveChangesAsync();

            SetUserContext(10); // Authenticate as Technician 10

            var request = new List<MobileApiController.VisitaCampoSyncDto>
            {
                new MobileApiController.VisitaCampoSyncDto
                {
                    OrdemServicoId = 1, // Belongs to Technician 99
                    CheckIn = DateTime.UtcNow
                }
            };

            // Act
            var result = await _controller.SyncVisitas(request);

            // Assert
            result.Should().BeOfType<ForbidResult>();

            var visitasInDb = await _context.VisitasCampo.ToListAsync();
            visitasInDb.Should().BeEmpty();
        }

        [Fact]
        public async Task SyncVisitas_ReturnsOk_WhenSyncingOwnOS()
        {
            // Arrange
            var osPropria = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.Recebido, TecnicoId = 10 };
            _context.OrdensServico.Add(osPropria);
            await _context.SaveChangesAsync();

            SetUserContext(10);

            var request = new List<MobileApiController.VisitaCampoSyncDto>
            {
                new MobileApiController.VisitaCampoSyncDto
                {
                    OrdemServicoId = 1,
                    CheckIn = DateTime.UtcNow
                }
            };

            // Act
            var result = await _controller.SyncVisitas(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();

            var visitasInDb = await _context.VisitasCampo.ToListAsync();
            visitasInDb.Should().HaveCount(1);
            visitasInDb.First().OrdemServicoId.Should().Be(1);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
