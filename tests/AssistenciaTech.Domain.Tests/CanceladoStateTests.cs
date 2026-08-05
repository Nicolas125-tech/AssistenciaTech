using System;
using Xunit;
using AssistenciaTech.Domain.Entities;
using AssistenciaTech.Domain.States;
using FluentAssertions;

namespace AssistenciaTech.Domain.Tests;

public class CanceladoStateTests
{
    [Fact]
    public void Avancar_DeveLancarExcecao()
    {
        // Arrange
        var os = new OrdemServico("OS-001", "123.456.789-00", "Modelo", "NS123", "Defeito");
        os.DefinirEstado(new CanceladoState());

        // Act
        Action act = () => os.AvancarStatus();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Uma ordem de serviço cancelada não pode avançar.");
    }

    [Fact]
    public void Cancelar_DeveLancarExcecao()
    {
        // Arrange
        var os = new OrdemServico("OS-001", "123.456.789-00", "Modelo", "NS123", "Defeito");
        os.DefinirEstado(new CanceladoState());

        // Act
        Action act = () => os.CancelarOS("Motivo");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("A ordem de serviço já está cancelada.");
    }

    [Fact]
    public void ObterStatusNome_DeveRetornarCancelado()
    {
        // Arrange
        var state = new CanceladoState();

        // Act
        var result = state.ObterStatusNome();

        // Assert
        result.Should().Be("Cancelado");
    }
}
