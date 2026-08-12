using System;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class EquipamentoBackupServiceTests
    {
        private readonly AppDbContext _context;
        private readonly EquipamentoBackupService _service;

        public EquipamentoBackupServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _service = new EquipamentoBackupService(_context);
        }

        [Fact]
        public async Task ProcessarTrocaEquipamentoAsync_ShouldMakeOldAvailable_WhenChanged()
        {
            // Arrange
            var equipamentoAntigo = new EquipamentoBackup { Id = 1, Descricao = "Antigo", Disponivel = false };
            _context.EquipamentosBackup.Add(equipamentoAntigo);
            await _context.SaveChangesAsync();

            // Act
            await _service.ProcessarTrocaEquipamentoAsync(1, null);
            await _context.SaveChangesAsync();

            // Assert
            var result = await _context.EquipamentosBackup.FindAsync(1);
            result.Should().NotBeNull();
            result!.Disponivel.Should().BeTrue();
        }

        [Fact]
        public async Task ProcessarTrocaEquipamentoAsync_ShouldMakeNewUnavailable_WhenAssigned()
        {
            // Arrange
            var equipamentoNovo = new EquipamentoBackup { Id = 2, Descricao = "Novo", Disponivel = true };
            _context.EquipamentosBackup.Add(equipamentoNovo);
            await _context.SaveChangesAsync();

            // Act
            await _service.ProcessarTrocaEquipamentoAsync(null, 2);
            await _context.SaveChangesAsync();

            // Assert
            var result = await _context.EquipamentosBackup.FindAsync(2);
            result.Should().NotBeNull();
            result!.Disponivel.Should().BeFalse();
        }

        [Fact]
        public async Task ProcessarTrocaEquipamentoAsync_ShouldDoBoth_WhenBothProvided()
        {
            // Arrange
            var equipamentoAntigo = new EquipamentoBackup { Id = 3, Descricao = "Antigo", Disponivel = false };
            var equipamentoNovo = new EquipamentoBackup { Id = 4, Descricao = "Novo", Disponivel = true };
            _context.EquipamentosBackup.AddRange(equipamentoAntigo, equipamentoNovo);
            await _context.SaveChangesAsync();

            // Act
            await _service.ProcessarTrocaEquipamentoAsync(3, 4);
            await _context.SaveChangesAsync();

            // Assert
            var resultAntigo = await _context.EquipamentosBackup.FindAsync(3);
            resultAntigo.Should().NotBeNull();
            resultAntigo!.Disponivel.Should().BeTrue();

            var resultNovo = await _context.EquipamentosBackup.FindAsync(4);
            resultNovo.Should().NotBeNull();
            resultNovo!.Disponivel.Should().BeFalse();
        }

        [Fact]
        public async Task ProcessarTrocaEquipamentoAsync_ShouldDoNothing_WhenIdsAreSame()
        {
            // Arrange
            var equipamento = new EquipamentoBackup { Id = 5, Descricao = "Mesmo", Disponivel = false };
            _context.EquipamentosBackup.Add(equipamento);
            await _context.SaveChangesAsync();

            // Act
            await _service.ProcessarTrocaEquipamentoAsync(5, 5);
            await _context.SaveChangesAsync();

            // Assert
            var result = await _context.EquipamentosBackup.FindAsync(5);
            result.Should().NotBeNull();
            result!.Disponivel.Should().BeFalse(); // still false because it exited early
        }
    }
}
