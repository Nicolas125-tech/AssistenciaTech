using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AssistenciaTech.Data.Tests
{
    public class AuditoriaInterceptorTests
    {
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly AuditoriaInterceptor _interceptor;
        private readonly DbContextOptions<AppDbContext> _dbContextOptions;

        public AuditoriaInterceptorTests()
        {
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _interceptor = new AuditoriaInterceptor(_httpContextAccessorMock.Object);

            var dbName = Guid.NewGuid().ToString(); // unique DB name for each test
            _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .AddInterceptors(_interceptor)
                .Options;
        }

        private void SetupHttpContext(string? userName, bool isAuthenticated)
        {
            var httpContext = new DefaultHttpContext();

            if (isAuthenticated)
            {
                var claims = new[] { new Claim(ClaimTypes.Name, userName ?? "TestUser") };
                var identity = new ClaimsIdentity(claims, "TestAuthType");
                httpContext.User = new ClaimsPrincipal(identity);
            }
            else
            {
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            }

            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
        }

        private Cliente CreateTestClient(AppDbContext context)
        {
            var cliente = new Cliente
            {
                Nome = "Test Client",
                Cpf = "12345678901",
                Email = "test@example.com",
                Telefone = "123456789"
            };
            context.Clientes.Add(cliente);
            context.SaveChanges();
            context.ChangeTracker.Clear();
            return cliente;
        }

        [Fact]
        public async Task SavingChangesAsync_WhenNewOrdemServicoAdded_ShouldCreateCreationAuditLog()
        {
            // Arrange
            SetupHttpContext("AdminTest", isAuthenticated: true);
            using var setupContext = new AppDbContext(_dbContextOptions);
            var cliente = CreateTestClient(setupContext);

            using var context = new AppDbContext(_dbContextOptions);
            var os = new OrdemServico
            {
                ClienteId = cliente.Id,
                Equipamento = "Test Equipment",
                ProblemaRelatado = "Test Defect",
                Status = "Aberto"
            };

            context.OrdensServico.Add(os);

            // Act
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            // Assert
            var auditLogs = await context.AuditoriaOS.ToListAsync();
            auditLogs.Should().HaveCount(1);
            var log = auditLogs.First();

            log.OrdemServicoId.Should().Be(os.Id);
            log.Usuario.Should().Be("AdminTest");
            log.CampoAlterado.Should().Be("CRIACAO_OS");
            log.ValorNovo.Should().Be("OS Criada");
            log.ValorAntigo.Should().Be("");
        }

        [Fact]
        public async Task SavingChangesAsync_WhenOrdemServicoModified_ShouldCreateAuditLogForModifiedFields()
        {
            // Arrange
            SetupHttpContext("TechUser", isAuthenticated: true);
            using var setupContext = new AppDbContext(_dbContextOptions);
            var cliente = CreateTestClient(setupContext);

            var os = new OrdemServico
            {
                ClienteId = cliente.Id,
                Equipamento = "Equip 1",
                ProblemaRelatado = "Defect 1",
                Status = "Aberto"
            };

            setupContext.OrdensServico.Add(os);
            await setupContext.SaveChangesAsync();
            setupContext.ChangeTracker.Clear();
            setupContext.AuditoriaOS.RemoveRange(setupContext.AuditoriaOS); // Clear creation logs
            await setupContext.SaveChangesAsync();

            using var testContext = new AppDbContext(_dbContextOptions);
            var osToModify = await testContext.OrdensServico.FirstAsync();

            // Act
            osToModify.Status = "Em Andamento";
            osToModify.Equipamento = "Equip 1 - Modified";
            await testContext.SaveChangesAsync();
            testContext.ChangeTracker.Clear();

            // Assert
            var auditLogs = await testContext.AuditoriaOS.ToListAsync();
            auditLogs.Should().HaveCount(2);

            var statusLog = auditLogs.Single(a => a.CampoAlterado == nameof(OrdemServico.Status));
            statusLog.Usuario.Should().Be("TechUser");
            statusLog.ValorAntigo.Should().Be("Aberto");
            statusLog.ValorNovo.Should().Be("Em Andamento");

            var equipLog = auditLogs.Single(a => a.CampoAlterado == nameof(OrdemServico.Equipamento));
            equipLog.ValorAntigo.Should().Be("Equip 1");
            equipLog.ValorNovo.Should().Be("Equip 1 - Modified");
        }

        [Fact]
        public async Task SavingChangesAsync_WhenNoModifications_ShouldNotCreateAuditLog()
        {
            // Arrange
            SetupHttpContext("Admin", isAuthenticated: true);
            using var setupContext = new AppDbContext(_dbContextOptions);
            var cliente = CreateTestClient(setupContext);

            var os = new OrdemServico
            {
                ClienteId = cliente.Id,
                Equipamento = "Equip 1",
                ProblemaRelatado = "Defect",
                Status = "Aberto"
            };

            setupContext.OrdensServico.Add(os);
            await setupContext.SaveChangesAsync();
            setupContext.ChangeTracker.Clear();
            setupContext.AuditoriaOS.RemoveRange(setupContext.AuditoriaOS);
            await setupContext.SaveChangesAsync();

            using var testContext = new AppDbContext(_dbContextOptions);
            var osToVerify = await testContext.OrdensServico.FirstAsync();

            // Act
            // Not modifying anything, just calling save
            await testContext.SaveChangesAsync();
            testContext.ChangeTracker.Clear();

            // Assert
            var auditLogs = await testContext.AuditoriaOS.ToListAsync();
            auditLogs.Should().BeEmpty();
        }

        [Fact]
        public async Task SavingChangesAsync_WhenNotAuthenticated_ShouldUseSistemaDesconhecido()
        {
            // Arrange
            SetupHttpContext(null, isAuthenticated: false);
            using var setupContext = new AppDbContext(_dbContextOptions);
            var cliente = CreateTestClient(setupContext);

            using var context = new AppDbContext(_dbContextOptions);
            var os = new OrdemServico
            {
                ClienteId = cliente.Id,
                Equipamento = "Equip",
                ProblemaRelatado = "Defect",
                Status = "Aberto"
            };

            context.OrdensServico.Add(os);

            // Act
            await context.SaveChangesAsync();

            // Assert
            var auditLog = await context.AuditoriaOS.FirstAsync();
            auditLog.Usuario.Should().Be("Sistema/Desconhecido");
        }

        [Fact]
        public async Task SavingChangesAsync_WhenModifiedValueIsIdentical_ShouldNotCreateAuditLog()
        {
            // Arrange
            SetupHttpContext("User", isAuthenticated: true);
            using var setupContext = new AppDbContext(_dbContextOptions);
            var cliente = CreateTestClient(setupContext);

            var os = new OrdemServico
            {
                ClienteId = cliente.Id,
                Equipamento = "Equip 1",
                ProblemaRelatado = "Defect",
                Status = "Aberto"
            };

            setupContext.OrdensServico.Add(os);
            await setupContext.SaveChangesAsync();
            setupContext.ChangeTracker.Clear();
            setupContext.AuditoriaOS.RemoveRange(setupContext.AuditoriaOS);
            await setupContext.SaveChangesAsync();

            using var testContext = new AppDbContext(_dbContextOptions);
            var osToModify = await testContext.OrdensServico.FirstAsync();

            // Act
            // Entity framework might mark it modified, but the string is identical
            osToModify.Equipamento = "Equip 1";
            testContext.Entry(osToModify).Property(x => x.Equipamento).IsModified = true; // force EF to see it as modified

            await testContext.SaveChangesAsync();

            // Assert
            var auditLogs = await testContext.AuditoriaOS.ToListAsync();
            auditLogs.Should().BeEmpty(); // Since oldValue == newValue
        }
    }
}
