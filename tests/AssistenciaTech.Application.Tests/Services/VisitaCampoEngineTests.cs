using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.DTOs;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class VisitaCampoEngineTests
    {
        private AppDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new AppDbContext(options);
        }

        private IConfiguration GetConfiguration()
        {
            var configData = new Dictionary<string, string?>
            {
                { "Jwt:Key", "super_secret_test_key_with_at_least_32_characters!" },
                { "Jwt:Issuer", "AssistenciaTechTest" },
                { "Jwt:Audience", "AssistenciaTechMobileTest" }
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
        }

        [Fact]
        public async Task AutenticarTecnicoAsync_ComCredenciaisValidas_DeveRetornarTokenEJwtValido()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var hasher = new PasswordHasher<Usuario>();
            var user = new Usuario { Id = 1, Username = "carlos.tecnico" };
            user.PasswordHash = hasher.HashPassword(user, "SenhaForte123!");
            var tecnico = new Tecnico { Id = 10, Nome = "carlos.tecnico" };

            context.Usuarios.Add(user);
            context.Tecnicos.Add(tecnico);
            await context.SaveChangesAsync();

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            // Act
            var result = await engine.AutenticarTecnicoAsync("carlos.tecnico", "SenhaForte123!");

            // Assert
            result.Sucesso.Should().BeTrue();
            result.TecnicoId.Should().Be(10);
            result.NomeTecnico.Should().Be("carlos.tecnico");
            result.Token.Should().NotBeNullOrEmpty();

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(result.Token);
            jwt.Issuer.Should().Be("AssistenciaTechTest");
        }

        [Fact]
        public async Task AutenticarTecnicoAsync_ComUsuarioInexistente_DeveRetornarFalha()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            // Act
            var result = await engine.AutenticarTecnicoAsync("usuario.fantasma", "123456");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.Mensagem.Should().Contain("inválidos");
        }

        [Fact]
        public async Task AutenticarTecnicoAsync_ComSenhaIncorreta_DeveRetornarFalha()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var hasher = new PasswordHasher<Usuario>();
            var user = new Usuario { Id = 1, Username = "carlos.tecnico" };
            user.PasswordHash = hasher.HashPassword(user, "SenhaCorreta!");
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            // Act
            var result = await engine.AutenticarTecnicoAsync("carlos.tecnico", "SenhaErrada!");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.Mensagem.Should().Contain("inválidos");
        }

        [Fact]
        public async Task RegistrarCheckInAsync_ComOsValidaETecnicoAtribuido_DeveCriarVisitaERetornarOk()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var os = new OrdemServico { Id = 100, TecnicoId = 5, Status = WorkflowStatus.EmReparo, DataEntrada = DateTime.UtcNow };
            context.OrdensServico.Add(os);
            await context.SaveChangesAsync();

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            // Act
            var result = await engine.RegistrarCheckInAsync(100, 5, -23.5505m, -46.6333m);

            // Assert
            result.Sucesso.Should().BeTrue();
            result.StatusCode.Should().Be(200);
            result.VisitaId.Should().NotBeNull();

            using var assertContext = GetDbContext(dbName);
            var visitaSalva = await assertContext.VisitasCampo.FindAsync(result.VisitaId);
            visitaSalva.Should().NotBeNull();
            visitaSalva!.OrdemServicoId.Should().Be(100);
            visitaSalva.TecnicoId.Should().Be(5);
            visitaSalva.Latitude.Should().Be(-23.5505m);
            visitaSalva.Longitude.Should().Be(-46.6333m);
            visitaSalva.CheckOut.Should().BeNull();
        }

        [Fact]
        public async Task RegistrarCheckInAsync_ComOsInexistente_DeveRetornarNaoEncontrado()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            // Act
            var result = await engine.RegistrarCheckInAsync(999, 5, -23.5505m, -46.6333m);

            // Assert
            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(404);
            result.Mensagem.Should().Contain("não encontrada");
        }

        [Fact]
        public async Task RegistrarCheckInAsync_ComOsDeOutroTecnico_DeveRetornarProibido()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var os = new OrdemServico { Id = 100, TecnicoId = 5, Status = WorkflowStatus.EmReparo, DataEntrada = DateTime.UtcNow };
            context.OrdensServico.Add(os);
            await context.SaveChangesAsync();

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            // Act
            var result = await engine.RegistrarCheckInAsync(100, 99, -23.5505m, -46.6333m);

            // Assert
            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task FinalizarVisitaAsync_ComDadosValidos_DeveAtualizarCheckoutAssinaturaEAvancarOS()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var os = new OrdemServico { Id = 200, TecnicoId = 7, Status = WorkflowStatus.EmReparo, DataEntrada = DateTime.UtcNow };
            var visita = new VisitaCampo { Id = 10, OrdemServicoId = 200, TecnicoId = 7, CheckIn = DateTime.UtcNow.AddHours(-1) };
            context.OrdensServico.Add(os);
            context.VisitasCampo.Add(visita);
            await context.SaveChangesAsync();

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            lifecycleMock.Setup(l => l.AvancarStatusAsync(200, WorkflowStatus.Concluido, "Reparo finalizado com sucesso.", "Carlos"))
                .ReturnsAsync(OrdemServicoResult.Ok(os));

            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            // Act
            var result = await engine.FinalizarVisitaAsync(200, 10, 7, "data:image/png;base64,iVBORw...", "Reparo finalizado com sucesso.", "Carlos");

            // Assert
            result.Sucesso.Should().BeTrue();
            result.StatusCode.Should().Be(200);

            using var assertContext = GetDbContext(dbName);
            var visitaAtualizada = await assertContext.VisitasCampo.FindAsync(10);
            visitaAtualizada!.CheckOut.Should().NotBeNull();
            visitaAtualizada.AssinaturaClienteBase64.Should().Be("data:image/png;base64,iVBORw...");

            lifecycleMock.Verify(l => l.AvancarStatusAsync(200, WorkflowStatus.Concluido, "Reparo finalizado com sucesso.", "Carlos"), Times.Once);
        }

        [Fact]
        public async Task FinalizarVisitaAsync_ComVisitaInexistenteOuOsIncompativel_DeveRetornarNaoEncontrado()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            // Act
            var result = await engine.FinalizarVisitaAsync(200, 999, 7, "assinatura", "laudo", "Carlos");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task FinalizarVisitaAsync_ComVisitaJaFinalizada_DeveRetornarInvalido()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var os = new OrdemServico { Id = 200, TecnicoId = 7, Status = WorkflowStatus.Concluido, DataEntrada = DateTime.UtcNow };
            var visita = new VisitaCampo { Id = 10, OrdemServicoId = 200, TecnicoId = 7, CheckIn = DateTime.UtcNow.AddHours(-2), CheckOut = DateTime.UtcNow.AddHours(-1) };
            context.OrdensServico.Add(os);
            context.VisitasCampo.Add(visita);
            await context.SaveChangesAsync();

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            // Act
            var result = await engine.FinalizarVisitaAsync(200, 10, 7, "assinatura", "laudo", "Carlos");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(400);
            result.Mensagem.Should().Contain("já finalizada");
        }

        [Fact]
        public async Task FinalizarVisitaAsync_ComTecnicoNaoAtribuido_DeveRetornarProibido()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var os = new OrdemServico { Id = 200, TecnicoId = 7, Status = WorkflowStatus.EmReparo, DataEntrada = DateTime.UtcNow };
            var visita = new VisitaCampo { Id = 10, OrdemServicoId = 200, TecnicoId = 7, CheckIn = DateTime.UtcNow.AddHours(-1) };
            context.OrdensServico.Add(os);
            context.VisitasCampo.Add(visita);
            await context.SaveChangesAsync();

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            // Act - Outro técnico (Id 88)
            var result = await engine.FinalizarVisitaAsync(200, 10, 88, "assinatura", "laudo", "Outro");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task FinalizarVisitaAsync_QuandoLifecycleFalhar_DeveRetornarInvalidoENaoFinalizarVisita()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var os = new OrdemServico { Id = 200, TecnicoId = 7, Status = WorkflowStatus.EmReparo, DataEntrada = DateTime.UtcNow };
            var visita = new VisitaCampo { Id = 10, OrdemServicoId = 200, TecnicoId = 7, CheckIn = DateTime.UtcNow.AddHours(-1) };
            context.OrdensServico.Add(os);
            context.VisitasCampo.Add(visita);
            await context.SaveChangesAsync();

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            lifecycleMock.Setup(l => l.AvancarStatusAsync(200, WorkflowStatus.Concluido, "laudo", "Carlos"))
                .ReturnsAsync(OrdemServicoResult.Falha("Transição de status inválida ou estoque insuficiente."));

            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            // Act
            var result = await engine.FinalizarVisitaAsync(200, 10, 7, "assinatura", "laudo", "Carlos");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(400);
            result.Mensagem.Should().Contain("estoque insuficiente");

            using var assertContext = GetDbContext(dbName);
            var visitaNaoFinalizada = await assertContext.VisitasCampo.FindAsync(10);
            visitaNaoFinalizada!.CheckOut.Should().BeNull();
        }

        [Fact]
        public async Task SincronizarVisitasOfflineAsync_ComVisitasNovas_DeveInserirERetornarMetricas()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            context.OrdensServico.AddRange(
                new OrdemServico { Id = 301, DataEntrada = DateTime.UtcNow },
                new OrdemServico { Id = 302, DataEntrada = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            var checkIn1 = new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);
            var checkIn2 = new DateTime(2026, 11, 6, 14, 0, 0, DateTimeKind.Utc);

            var dtos = new List<VisitaCampoSyncDto>
            {
                new VisitaCampoSyncDto { OfflineId = "OFF-1", OrdemServicoId = 301, CheckIn = checkIn1, Latitude = -23.5m, Longitude = -46.6m },
                new VisitaCampoSyncDto { OfflineId = "OFF-2", OrdemServicoId = 302, CheckIn = checkIn2, Latitude = -23.6m, Longitude = -46.7m }
            };

            // Act
            var result = await engine.SincronizarVisitasOfflineAsync(12, dtos);

            // Assert
            result.Sucesso.Should().BeTrue();
            result.TotalProcessadas.Should().Be(2);
            result.TotalInseridas.Should().Be(2);
            result.TotalDuplicadas.Should().Be(0);

            using var assertContext = GetDbContext(dbName);
            var visitasSalvas = await assertContext.VisitasCampo.Where(v => v.TecnicoId == 12).ToListAsync();
            visitasSalvas.Should().HaveCount(2);
        }

        [Fact]
        public async Task SincronizarVisitasOfflineAsync_ComVisitasDuplicadas_DeveIgnorarDuplicatas()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var checkIn = new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);
            var visitaExistente = new VisitaCampo { Id = 1, OrdemServicoId = 301, TecnicoId = 12, CheckIn = checkIn };
            context.OrdensServico.Add(new OrdemServico { Id = 301, DataEntrada = DateTime.UtcNow });
            context.VisitasCampo.Add(visitaExistente);
            await context.SaveChangesAsync();

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            var dtos = new List<VisitaCampoSyncDto>
            {
                new VisitaCampoSyncDto { OfflineId = "OFF-1", OrdemServicoId = 301, CheckIn = checkIn } // Duplicada!
            };

            // Act
            var result = await engine.SincronizarVisitasOfflineAsync(12, dtos);

            // Assert
            result.Sucesso.Should().BeTrue();
            result.TotalProcessadas.Should().Be(1);
            result.TotalInseridas.Should().Be(0);
            result.TotalDuplicadas.Should().Be(1);

            using var assertContext = GetDbContext(dbName);
            var totalVisitas = await assertContext.VisitasCampo.CountAsync();
            totalVisitas.Should().Be(1);
        }

        [Fact]
        public async Task SincronizarVisitasOfflineAsync_ComListaVazia_DeveRetornarFalha()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var config = GetConfiguration();
            var lifecycleMock = new Mock<IOrdemServicoLifecycle>();
            var loggerMock = new Mock<ILogger<VisitaCampoEngine>>();
            var engine = new VisitaCampoEngine(context, config, lifecycleMock.Object, loggerMock.Object);

            // Act
            var result = await engine.SincronizarVisitasOfflineAsync(12, new List<VisitaCampoSyncDto>());

            // Assert
            result.Sucesso.Should().BeFalse();
            result.Mensagem.Should().Contain("Nenhum dado");
        }
    }
}
