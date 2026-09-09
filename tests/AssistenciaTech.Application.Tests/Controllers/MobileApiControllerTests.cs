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
        public async Task CheckIn_ReturnsUnauthorized_WhenClaimIdIsNotValidInteger()
        {
            // Arrange
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "invalid_id")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            var request = new MobileApiController.CheckInRequest { Latitude = 10m, Longitude = 20m };

            // Act
            var result = await _controller.CheckIn(1, request);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().BeEquivalentTo(new { error = "Técnico não autenticado." });
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
        public async Task FinalizarVisita_ReturnsUnauthorized_WhenClaimIdIsNotValidInteger()
        {
            // Arrange
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "invalid_id")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            var request = new MobileApiController.FinalizarRequest { VisitaId = 1 };

            // Act
            var result = await _controller.FinalizarVisita(1, request);

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

        [Fact]
        public async Task FinalizarVisita_ReturnsNotFound_WhenVisitaDoesNotBelongToOS()
        {
            // Arrange
            var visita = new VisitaCampo
            {
                Id = 1,
                OrdemServicoId = 2, // Different OS
                TecnicoId = 10,
                CheckIn = DateTime.UtcNow
            };
            _context.VisitasCampo.Add(visita);
            await _context.SaveChangesAsync();

            SetUserContext(10);

            var request = new MobileApiController.FinalizarRequest { VisitaId = 1 };

            // Act
            var result = await _controller.FinalizarVisita(1, request); // Mismatched OS ID

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().BeEquivalentTo(new { error = "Visita não encontrada ou não pertence a esta OS" });
        }

        [Fact]
        public async Task FinalizarVisita_UpdatesLaudoAndAssinatura_WhenProvided()
        {
            // Arrange
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.EmAnalise, LaudoTecnico = "Original" };
            _context.OrdensServico.Add(os);

            var visita = new VisitaCampo
            {
                Id = 1,
                OrdemServicoId = 1,
                TecnicoId = 10,
                CheckIn = DateTime.UtcNow
            };
            _context.VisitasCampo.Add(visita);
            await _context.SaveChangesAsync();

            SetUserContext(10);

            var request = new MobileApiController.FinalizarRequest
            {
                VisitaId = 1,
                AssinaturaBase64 = "base64string",
                LaudoFinal = "Novo Laudo"
            };

            // Act
            var result = await _controller.FinalizarVisita(1, request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();

            var osInDb = await _context.OrdensServico.FindAsync(1);
            osInDb!.LaudoTecnico.Should().Be("Novo Laudo");
            osInDb.Status.Should().Be(WorkflowStatus.Concluido);

            var visitaInDb = await _context.VisitasCampo.FindAsync(1);
            visitaInDb!.AssinaturaClienteBase64.Should().Be("base64string");
        }

        [Fact]
        public async Task FinalizarVisita_DoesNotUpdateLaudo_WhenLaudoFinalIsEmpty()
        {
            // Arrange
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.EmAnalise, LaudoTecnico = "Original" };
            _context.OrdensServico.Add(os);

            var visita = new VisitaCampo
            {
                Id = 1,
                OrdemServicoId = 1,
                TecnicoId = 10,
                CheckIn = DateTime.UtcNow
            };
            _context.VisitasCampo.Add(visita);
            await _context.SaveChangesAsync();

            SetUserContext(10);

            var request = new MobileApiController.FinalizarRequest
            {
                VisitaId = 1,
                AssinaturaBase64 = "base64string",
                LaudoFinal = "" // Empty
            };

            // Act
            var result = await _controller.FinalizarVisita(1, request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();

            var osInDb = await _context.OrdensServico.FindAsync(1);
            osInDb!.LaudoTecnico.Should().Be("Original"); // Should remain unchanged
            osInDb.Status.Should().Be(WorkflowStatus.Concluido);

            var visitaInDb = await _context.VisitasCampo.FindAsync(1);
            visitaInDb!.AssinaturaClienteBase64.Should().Be("base64string");
        }

        [Fact]
        public async Task Login_ReturnsOk_WhenCredentialsAreValid()
        {
            // Arrange
            var user = new Usuario
            {
                Username = "admin",
                PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<Usuario>().HashPassword(null, "Admin@123")
            };
            _context.Usuarios.Add(user);

            var tecnico = new Tecnico
            {
                Nome = "admin",
                PercentualComissao = 10,
                Ativo = true
            };
            _context.Tecnicos.Add(tecnico);
            await _context.SaveChangesAsync();

            var mockConfiguration = new Moq.Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            mockConfiguration.Setup(x => x["Jwt:Key"]).Returns("UmaChaveSuperSecretaMuitoLongaParaOJWT123456789_AppMobile_AssistenciaTech_IsNowRequired_123456789_AppMobile_AssistenciaTech!");
            mockConfiguration.Setup(x => x["Jwt:Issuer"]).Returns("AssistenciaTech");
            mockConfiguration.Setup(x => x["Jwt:Audience"]).Returns("AssistenciaTechMobile");

            var controller = new MobileApiController(_context, mockConfiguration.Object);

            var request = new MobileApiController.MobileLoginRequest
            {
                Username = "admin",
                Password = "Admin@123"
            };

            // Act
            var result = await controller.Login(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task Login_ReturnsUnauthorized_WhenCredentialsAreInvalid()
        {
            // Arrange
            var user = new Usuario
            {
                Username = "admin",
                PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<Usuario>().HashPassword(null, "Admin@123")
            };
            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();

            var mockConfiguration = new Moq.Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            mockConfiguration.Setup(x => x["Jwt:Key"]).Returns("UmaChaveSuperSecretaMuitoLongaParaOJWT123456789_AppMobile_AssistenciaTech_IsNowRequired_123456789_AppMobile_AssistenciaTech!");
            var controller = new MobileApiController(_context, mockConfiguration.Object);

            var request = new MobileApiController.MobileLoginRequest
            {
                Username = "admin",
                Password = "wrongpassword"
            };

            // Act
            var result = await controller.Login(request);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().BeEquivalentTo(new { error = "Usuário ou senha inválidos." });
        }
        [Fact]
        public async Task SyncVisitas_ReturnsUnauthorized_WhenUserIsNotAuthenticated()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var dtoList = new System.Collections.Generic.List<MobileApiController.VisitaCampoSyncDto>();

            // Act
            var result = await _controller.SyncVisitas(dtoList);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().BeEquivalentTo(new { error = "Técnico não autenticado." });
        }

        [Fact]
        public async Task SyncVisitas_ReturnsBadRequest_WhenDtoListIsNull()
        {
            // Arrange
            SetUserContext(10);

            // Act
            var result = await _controller.SyncVisitas(null!);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { error = "Nenhum dado para sincronizar." });
        }

        [Fact]
        public async Task SyncVisitas_ReturnsBadRequest_WhenDtoListIsEmpty()
        {
            // Arrange
            SetUserContext(10);
            var dtoList = new System.Collections.Generic.List<MobileApiController.VisitaCampoSyncDto>();

            // Act
            var result = await _controller.SyncVisitas(dtoList);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { error = "Nenhum dado para sincronizar." });
        }

        [Fact]
        public async Task SyncVisitas_ReturnsOkAndSavesData_WhenProvidedValidData()
        {
            // Arrange
            SetUserContext(10);
            var dtoList = new System.Collections.Generic.List<MobileApiController.VisitaCampoSyncDto>
            {
                new MobileApiController.VisitaCampoSyncDto
                {
                    OrdemServicoId = 100,
                    CheckIn = new DateTime(2023, 10, 1, 10, 0, 0, DateTimeKind.Utc),
                    CheckOut = new DateTime(2023, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                    Latitude = -23.5505m,
                    Longitude = -46.6333m,
                    AssinaturaClienteBase64 = "base64string1"
                },
                new MobileApiController.VisitaCampoSyncDto
                {
                    OrdemServicoId = 101,
                    CheckIn = new DateTime(2023, 10, 2, 14, 0, 0, DateTimeKind.Utc),
                    Latitude = -22.9068m,
                    Longitude = -43.1729m
                }
            };

            // Act
            var result = await _controller.SyncVisitas(dtoList);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new { status = "success", message = "2 visitas sincronizadas com sucesso." });

            var visitas = await _context.VisitasCampo.ToListAsync();
            visitas.Should().HaveCount(2);

            var visita1 = visitas.First(v => v.OrdemServicoId == 100);
            visita1.TecnicoId.Should().Be(10);
            visita1.CheckIn.Should().Be(new DateTime(2023, 10, 1, 10, 0, 0, DateTimeKind.Utc));
            visita1.CheckOut.Should().Be(new DateTime(2023, 10, 1, 12, 0, 0, DateTimeKind.Utc));
            visita1.Latitude.Should().Be(-23.5505m);
            visita1.Longitude.Should().Be(-46.6333m);
            visita1.AssinaturaClienteBase64.Should().Be("base64string1");

            var visita2 = visitas.First(v => v.OrdemServicoId == 101);
            visita2.TecnicoId.Should().Be(10);
            visita2.CheckIn.Should().Be(new DateTime(2023, 10, 2, 14, 0, 0, DateTimeKind.Utc));
            visita2.CheckOut.Should().BeNull();
            visita2.Latitude.Should().Be(-22.9068m);
            visita2.Longitude.Should().Be(-43.1729m);
            visita2.AssinaturaClienteBase64.Should().BeNull();
        }




        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
