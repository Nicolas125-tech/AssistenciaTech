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
    // A custom DbContext that overrides SaveChanges(Async) to simulate an exception
    // without needing to instantiate abstract EventDefinitionBase objects.
    public class FailingDbContext : AppDbContext
    {
        public bool ShouldFail { get; set; } = false;

        public FailingDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public override int SaveChanges()
        {
            if (ShouldFail)
                throw new DbUpdateException("Simulated failure");

            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
                throw new DbUpdateException("Simulated failure");

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    public class AuditoriaInterceptorTests
    {
        private AppDbContext CreateContext(AuditoriaInterceptor interceptor)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options;

            return new AppDbContext(options);
        }

        private FailingDbContext CreateFailingContext(AuditoriaInterceptor interceptor)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options;

            return new FailingDbContext(options);
        }

        private Mock<IHttpContextAccessor> SetupHttpContextAccessor(string? username = null)
        {
            var mockAccessor = new Mock<IHttpContextAccessor>();
            if (username != null)
            {
                var claims = new[] { new Claim(ClaimTypes.Name, username) };
                var identity = new ClaimsIdentity(claims, "TestAuthType");
                var claimsPrincipal = new ClaimsPrincipal(identity);

                var mockHttpContext = new Mock<HttpContext>();
                mockHttpContext.Setup(c => c.User).Returns(claimsPrincipal);

                mockAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);
            }
            else
            {
                mockAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
            }

            return mockAccessor;
        }

        [Fact]
        public async Task SavingChangesAsync_WhenAddingOrdemServico_ShouldCreateAuditLogForCreation()
        {
            // Arrange
            var mockAccessor = SetupHttpContextAccessor("TestUser");
            var interceptor = new AuditoriaInterceptor(mockAccessor.Object);
            await using var context = CreateContext(interceptor);

            var os = new OrdemServico
            {
                ClienteId = 1,
                Equipamento = "Test Laptop",
                ProblemaRelatado = "Won't turn on",
                Status = "Recebido"
            };

            // Act
            context.OrdensServico.Add(os);
            await context.SaveChangesAsync();

            // Assert
            var audit = await context.AuditoriaOS.SingleOrDefaultAsync(a => a.OrdemServicoId == os.Id);
            audit.Should().NotBeNull();
            audit!.Usuario.Should().Be("TestUser");
            audit.CampoAlterado.Should().Be("CRIACAO_OS");
            audit.ValorAntigo.Should().Be("");
            audit.ValorNovo.Should().Be("OS Criada");
            audit.DataAlteracao.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task SavingChangesAsync_WhenModifyingOrdemServico_ShouldCreateAuditLogsForModifiedProperties()
        {
            // Arrange
            var mockAccessor = SetupHttpContextAccessor("TestUser");
            var interceptor = new AuditoriaInterceptor(mockAccessor.Object);
            await using var context = CreateContext(interceptor);

            var os = new OrdemServico
            {
                ClienteId = 1,
                Equipamento = "Test Laptop",
                ProblemaRelatado = "Won't turn on",
                Status = "Recebido"
            };

            context.OrdensServico.Add(os);
            await context.SaveChangesAsync(); // Initial save creates the CRIACAO_OS audit

            // Clear the change tracker so it acts like a new request fetching the entity
            context.ChangeTracker.Clear();

            // Act
            var osToModify = await context.OrdensServico.FirstAsync();
            osToModify.Status = "Em Andamento";
            osToModify.AnotacoesInternas = "Test Note";

            await context.SaveChangesAsync();

            // Assert
            var audits = await context.AuditoriaOS
                .Where(a => a.OrdemServicoId == os.Id && a.CampoAlterado != "CRIACAO_OS")
                .ToListAsync();

            audits.Should().HaveCount(2);

            var statusAudit = audits.Single(a => a.CampoAlterado == "Status");
            statusAudit.Usuario.Should().Be("TestUser");
            statusAudit.ValorAntigo.Should().Be("Recebido");
            statusAudit.ValorNovo.Should().Be("Em Andamento");

            var noteAudit = audits.Single(a => a.CampoAlterado == "AnotacoesInternas");
            noteAudit.Usuario.Should().Be("TestUser");
            noteAudit.ValorAntigo.Should().BeNull(); // It was empty/null initially
            noteAudit.ValorNovo.Should().Be("Test Note");
        }

        [Fact]
        public async Task SavingChangesAsync_WhenModifyingOrdemServicoWithoutChanges_ShouldNotCreateAuditLog()
        {
            // Arrange
            var mockAccessor = SetupHttpContextAccessor("TestUser");
            var interceptor = new AuditoriaInterceptor(mockAccessor.Object);
            await using var context = CreateContext(interceptor);

            var os = new OrdemServico
            {
                ClienteId = 1,
                Equipamento = "Test Laptop",
                ProblemaRelatado = "Won't turn on",
                Status = "Recebido"
            };

            context.OrdensServico.Add(os);
            await context.SaveChangesAsync(); // Initial save creates the CRIACAO_OS audit

            context.ChangeTracker.Clear();

            // Act
            var osToModify = await context.OrdensServico.FirstAsync();
            osToModify.Status = "Recebido"; // No change

            // Mark entity as modified to trigger interceptor, but with no property changes
            context.Entry(osToModify).State = EntityState.Modified;

            await context.SaveChangesAsync();

            // Assert
            var audits = await context.AuditoriaOS
                .Where(a => a.OrdemServicoId == os.Id && a.CampoAlterado != "CRIACAO_OS")
                .ToListAsync();

            audits.Should().BeEmpty();
        }

        [Fact]
        public async Task SavingChangesAsync_WhenNoHttpContext_ShouldUseDefaultUser()
        {
            // Arrange
            var mockAccessor = SetupHttpContextAccessor(null);
            var interceptor = new AuditoriaInterceptor(mockAccessor.Object);
            await using var context = CreateContext(interceptor);

            var os = new OrdemServico
            {
                ClienteId = 1,
                Equipamento = "Test Laptop",
                ProblemaRelatado = "Won't turn on",
                Status = "Recebido"
            };

            // Act
            context.OrdensServico.Add(os);
            await context.SaveChangesAsync();

            // Assert
            var audit = await context.AuditoriaOS.SingleAsync();
            audit.Usuario.Should().Be("Sistema/Desconhecido");
        }

        [Fact]
        public async Task SaveChangesFailedAsync_ShouldClearPendingAudits()
        {
            // Arrange
            var mockAccessor = SetupHttpContextAccessor("TestUser");
            var interceptor = new AuditoriaInterceptor(mockAccessor.Object);
            await using var context = CreateFailingContext(interceptor);

            var os = new OrdemServico
            {
                ClienteId = 1,
                Equipamento = "Test Laptop",
                Status = "Recebido"
            };

            // Act
            context.OrdensServico.Add(os);
            context.ShouldFail = true;

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Expected to fail
            }

            // Assert
            // To test if it cleared pending audits, we can verify it doesn't process them on a subsequent successful save
            var validOs = new OrdemServico
            {
                ClienteId = 1,
                Equipamento = "Valid Laptop",
                ProblemaRelatado = "Valid Problem",
                Status = "Recebido"
            };

            context.ShouldFail = false;
            context.ChangeTracker.Clear();
            context.OrdensServico.Add(validOs);
            await context.SaveChangesAsync();

            // Only one audit should exist - for the valid OS
            var audits = await context.AuditoriaOS.ToListAsync();
            audits.Should().HaveCount(1);
            audits[0].OrdemServicoId.Should().Be(validOs.Id);
        }

        [Fact]
        public void SaveChanges_WhenAddingOrdemServico_ShouldCreateAuditLogForCreation()
        {
            // Arrange
            var mockAccessor = SetupHttpContextAccessor("TestUser");
            var interceptor = new AuditoriaInterceptor(mockAccessor.Object);
            using var context = CreateContext(interceptor);

            var os = new OrdemServico
            {
                ClienteId = 1,
                Equipamento = "Test Laptop",
                ProblemaRelatado = "Won't turn on",
                Status = "Recebido"
            };

            // Act
            context.OrdensServico.Add(os);
            context.SaveChanges(); // Sync version

            // Assert
            var audit = context.AuditoriaOS.SingleOrDefault(a => a.OrdemServicoId == os.Id);
            audit.Should().NotBeNull();
            audit!.Usuario.Should().Be("TestUser");
            audit.CampoAlterado.Should().Be("CRIACAO_OS");
            audit.ValorAntigo.Should().Be("");
            audit.ValorNovo.Should().Be("OS Criada");
            audit.DataAlteracao.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void SaveChangesFailed_ShouldClearPendingAudits()
        {
            // Arrange
            var mockAccessor = SetupHttpContextAccessor("TestUser");
            var interceptor = new AuditoriaInterceptor(mockAccessor.Object);
            using var context = CreateFailingContext(interceptor);

            var os = new OrdemServico
            {
                ClienteId = 1,
                Equipamento = "Test Laptop",
                Status = "Recebido"
            };

            // Act
            context.OrdensServico.Add(os);
            context.ShouldFail = true;

            try
            {
                context.SaveChanges();
            }
            catch (DbUpdateException)
            {
                // Expected to fail
            }

            // Assert
            // To test if it cleared pending audits, we can verify it doesn't process them on a subsequent successful save
            var validOs = new OrdemServico
            {
                ClienteId = 1,
                Equipamento = "Valid Laptop",
                ProblemaRelatado = "Valid Problem",
                Status = "Recebido"
            };

            context.ShouldFail = false;
            context.ChangeTracker.Clear();
            context.OrdensServico.Add(validOs);
            context.SaveChanges();

            // Only one audit should exist - for the valid OS
            var audits = context.AuditoriaOS.ToList();
            audits.Should().HaveCount(1);
            audits[0].OrdemServicoId.Should().Be(validOs.Id);
        }
    }
}
