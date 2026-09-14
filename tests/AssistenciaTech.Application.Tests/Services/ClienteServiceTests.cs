using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class ClienteServiceTests
    {
        private AppDbContext GetMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task GetClientesSelectListAsync_ReturnsEmptyList_WhenNoClientesExist()
        {
            // Arrange
            using var context = GetMemoryContext(nameof(GetClientesSelectListAsync_ReturnsEmptyList_WhenNoClientesExist));
            var service = new ClienteService(context);

            // Act
            var result = await service.GetClientesSelectListAsync();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetClientesSelectListAsync_ReturnsFormattedSelectListItem_ForExistingClientes()
        {
            // Arrange
            using var context = GetMemoryContext(nameof(GetClientesSelectListAsync_ReturnsFormattedSelectListItem_ForExistingClientes));
            var cliente = new Cliente
            {
                Nome = "João Silva",
                Cpf = "11122233344",
                Telefone = "11999998888"
            };
            context.Clientes.Add(cliente);
            await context.SaveChangesAsync();

            var service = new ClienteService(context);

            // Act
            var result = await service.GetClientesSelectListAsync();

            // Assert
            result.Should().HaveCount(1);
            var item = result.First();
            item.Value.Should().Be(cliente.Id.ToString());
            item.Text.Should().Be("João Silva - CPF: 11122233344 - Tel: 11999998888");
            item.Selected.Should().BeFalse();
        }

        [Fact]
        public async Task GetClientesSelectListAsync_DoesNotSetSelected_WhenSelectedIdIsNull()
        {
            // Arrange
            using var context = GetMemoryContext(nameof(GetClientesSelectListAsync_DoesNotSetSelected_WhenSelectedIdIsNull));
            context.Clientes.Add(new Cliente { Nome = "Maria", Cpf = "111", Telefone = "111" });
            context.Clientes.Add(new Cliente { Nome = "José", Cpf = "222", Telefone = "222" });
            await context.SaveChangesAsync();

            var service = new ClienteService(context);

            // Act
            var result = await service.GetClientesSelectListAsync(selectedId: null);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(x => x.Selected == false);
        }

        [Fact]
        public async Task GetClientesSelectListAsync_SetsSelectedTrue_ForMatchingSelectedId()
        {
            // Arrange
            using var context = GetMemoryContext(nameof(GetClientesSelectListAsync_SetsSelectedTrue_ForMatchingSelectedId));
            var cliente1 = new Cliente { Nome = "Maria", Cpf = "111", Telefone = "111" };
            var cliente2 = new Cliente { Nome = "José", Cpf = "222", Telefone = "222" };

            context.Clientes.Add(cliente1);
            context.Clientes.Add(cliente2);
            await context.SaveChangesAsync();

            var service = new ClienteService(context);

            // Act
            var result = await service.GetClientesSelectListAsync(selectedId: cliente2.Id);

            // Assert
            result.Should().HaveCount(2);
            var item1 = result.First(x => x.Value == cliente1.Id.ToString());
            var item2 = result.First(x => x.Value == cliente2.Id.ToString());

            item1.Selected.Should().BeFalse();
            item2.Selected.Should().BeTrue();
        }
    }
}
