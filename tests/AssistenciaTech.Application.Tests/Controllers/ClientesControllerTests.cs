using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    // Custom DbContext to simulate DbUpdateConcurrencyException
    public class ConcurrencyAppDbContext : AppDbContext
    {
        public bool ThrowConcurrencyException { get; set; } = false;
        public bool RemoveEntityOnException { get; set; } = false;

        public ConcurrencyAppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowConcurrencyException)
            {
                if (RemoveEntityOnException)
                {
                    var clienteEntity = ChangeTracker.Entries<Cliente>().FirstOrDefault(e => e.State == EntityState.Modified);
                    if (clienteEntity != null)
                    {
                        clienteEntity.State = EntityState.Detached;
                        var cliente = Set<Cliente>().Local.FirstOrDefault(c => c.Id == clienteEntity.Entity.Id) ??
                                      Set<Cliente>().FirstOrDefault(c => c.Id == clienteEntity.Entity.Id);

                        if (cliente != null)
                        {
                            Set<Cliente>().Remove(cliente);
                            base.SaveChanges();
                        }
                    }

                    var tecnicoEntity = ChangeTracker.Entries<Tecnico>().FirstOrDefault(e => e.State == EntityState.Modified);
                    if (tecnicoEntity != null)
                    {
                        tecnicoEntity.State = EntityState.Detached;
                        var tecnico = Set<Tecnico>().Local.FirstOrDefault(c => c.Id == tecnicoEntity.Entity.Id) ??
                                      Set<Tecnico>().FirstOrDefault(c => c.Id == tecnicoEntity.Entity.Id);

                        if (tecnico != null)
                        {
                            Set<Tecnico>().Remove(tecnico);
                            base.SaveChanges();
                        }
                    }
                }
                throw new DbUpdateConcurrencyException("Simulated concurrency exception");
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    public class ClientesControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly ClientesController _controller;
        private readonly DbContextOptions<AppDbContext> _options;

        public ClientesControllerTests()
        {
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(_options);
            _controller = new ClientesController(_context);
        }

        [Fact]
        public async Task Index_ShouldReturnViewWithClientesList()
        {
            // Arrange
            _context.Clientes.Add(new Cliente { Nome = "Cliente 1", Cpf = "11111111111", Telefone = "11999999999", Email = "cliente1@teste.com" });
            _context.Clientes.Add(new Cliente { Nome = "Cliente 2", Cpf = "22222222222", Telefone = "11888888888", Email = "cliente2@teste.com" });
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<Cliente>>().Subject;
            model.Should().HaveCount(2);
        }

        [Fact]
        public void Create_Get_ShouldReturnView()
        {
            // Act
            var result = _controller.Create();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public async Task Create_Post_ValidModel_ShouldAddClienteAndRedirectToIndex()
        {
            // Arrange
            var novoCliente = new Cliente
            {
                Nome = "Novo Cliente",
                Cpf = "33333333333",
                Telefone = "11777777777",
                Email = "novo@teste.com"
            };

            // Act
            var result = await _controller.Create(novoCliente);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");

            var clienteInDb = await _context.Clientes.FirstOrDefaultAsync(c => c.Cpf == "33333333333");
            clienteInDb.Should().NotBeNull();
            clienteInDb.Nome.Should().Be("Novo Cliente");
        }

        [Fact]
        public async Task Create_Post_InvalidModel_ShouldReturnViewWithModel_AndNotSaveToDb()
        {
            // Arrange
            var clienteInvalido = new Cliente
            {
                Cpf = "33333333333",
                Telefone = "11777777777",
                Email = "novo@teste.com"
            };
            _controller.ModelState.AddModelError("Nome", "O nome é obrigatório.");

            // Act
            var result = await _controller.Create(clienteInvalido);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeEquivalentTo(clienteInvalido);

            var clientesInDb = await _context.Clientes.ToListAsync();
            clientesInDb.Should().BeEmpty();
        }

        [Fact]
        public async Task Edit_Get_NullId_ShouldReturnNotFound()
        {
            // Act
            var result = await _controller.Edit((int?)null);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Get_InvalidId_ShouldReturnNotFound()
        {
            // Act
            var result = await _controller.Edit(999);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Get_ValidId_ShouldReturnViewWithCliente()
        {
            // Arrange
            var cliente = new Cliente { Nome = "Cliente Teste", Cpf = "11111111111", Telefone = "11999999999" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.Edit(cliente.Id);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<Cliente>().Subject;
            model.Id.Should().Be(cliente.Id);
            model.Nome.Should().Be("Cliente Teste");
        }

        [Fact]
        public async Task Edit_Post_IdMismatch_ShouldReturnNotFound()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Cliente" };

            // Act
            var result = await _controller.Edit(2, cliente);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_ShouldReturnViewWithModel()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Cliente" };
            _controller.ModelState.AddModelError("Cpf", "CPF é obrigatório");

            // Act
            var result = await _controller.Edit(1, cliente);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeEquivalentTo(cliente);
        }

        [Fact]
        public async Task Edit_Post_ValidModel_ShouldUpdateClienteAndRedirectToIndex()
        {
            // Arrange
            var cliente = new Cliente { Nome = "Nome Antigo", Cpf = "11111111111", Telefone = "11999999999" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var clienteAtualizado = new Cliente
            {
                Id = cliente.Id,
                Nome = "Nome Novo",
                Cpf = "11111111111",
                Telefone = "11888888888",
                Email = "novo@teste.com"
            };

            // Act
            var result = await _controller.Edit(cliente.Id, clienteAtualizado);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");

            var clienteInDb = await _context.Clientes.FindAsync(cliente.Id);
            clienteInDb.Should().NotBeNull();
            clienteInDb.Nome.Should().Be("Nome Novo");
            clienteInDb.Telefone.Should().Be("11888888888");
        }

        [Fact]
        public async Task Edit_Post_ConcurrencyException_ClienteDeleted_ShouldReturnNotFound()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var concurrencyContext = new ConcurrencyAppDbContext(options);
            using var controller = new ClientesController(concurrencyContext);

            var cliente = new Cliente { Nome = "Old", Cpf = "111", Telefone = "111" };
            concurrencyContext.Clientes.Add(cliente);
            await concurrencyContext.SaveChangesAsync();
            concurrencyContext.ChangeTracker.Clear();

            var clienteAtualizado = new Cliente { Id = cliente.Id, Nome = "Nome Novo", Cpf = "111", Telefone = "111" };
            concurrencyContext.ThrowConcurrencyException = true;
            concurrencyContext.RemoveEntityOnException = true;

            // Act
            var result = await controller.Edit(cliente.Id, clienteAtualizado);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_Post_ConcurrencyException_ClienteExists_ShouldThrow()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var concurrencyContext = new ConcurrencyAppDbContext(options);
            using var controller = new ClientesController(concurrencyContext);

            var cliente = new Cliente { Nome = "Old", Cpf = "111", Telefone = "111" };
            concurrencyContext.Clientes.Add(cliente);
            await concurrencyContext.SaveChangesAsync();
            concurrencyContext.ChangeTracker.Clear();

            var clienteAtualizado = new Cliente { Id = cliente.Id, Nome = "Nome Novo", Cpf = "111", Telefone = "111" };
            concurrencyContext.ThrowConcurrencyException = true;
            concurrencyContext.RemoveEntityOnException = false; // Entity still exists

            // Act & Assert
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => controller.Edit(cliente.Id, clienteAtualizado));
        }

        [Fact]
        public async Task Delete_Post_ValidId_NoOrdensServico_ShouldRemoveClienteAndRedirect()
        {
            // Arrange
            var cliente = new Cliente { Nome = "Cliente Teste", Cpf = "11111111111", Telefone = "11999999999" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.Delete(cliente.Id);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");

            var clienteInDb = await _context.Clientes.FindAsync(cliente.Id);
            clienteInDb.Should().BeNull();
        }

        [Fact]
        public async Task Delete_Post_ValidId_HasOrdensServico_ShouldNotRemoveAndRedirectWithError()
        {
            // Arrange
            var cliente = new Cliente { Nome = "Cliente Teste", Cpf = "11111111111", Telefone = "11999999999" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var os = new OrdemServico { ClienteId = cliente.Id, ProblemaRelatado = "Problema" };
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.Delete(cliente.Id);

            // Assert
            var redirectToActionResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectToActionResult.ActionName.Should().Be("Index");
            redirectToActionResult.RouteValues.Should().ContainKey("erro");
            redirectToActionResult.RouteValues["erro"].ToString().Should().Contain("Não é possível excluir um cliente com Ordens de Serviço associadas.");

            var clienteInDb = await _context.Clientes.FindAsync(cliente.Id);
            clienteInDb.Should().NotBeNull();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
