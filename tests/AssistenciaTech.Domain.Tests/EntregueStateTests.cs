using System;
using Xunit;
using AssistenciaTech.Domain.Entities;
using AssistenciaTech.Domain.States;
using FluentAssertions;

namespace AssistenciaTech.Domain.Tests;

public class EntregueStateTests
{
    [Fact]
    public void Avancar_DeveLancarExcecao()
    {
        // Arrange
        var state = new EntregueState();
        var os = new OrdemServico("OS-001", "123.456.789-00", "Modelo", "NS123", "Defeito");

        // Act
        Action act = () => state.Avancar(os);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("A ordem de serviço já foi finalizada e entregue.");
    }

    [Fact]
    public void Cancelar_DeveLancarExcecao()
    {
        // Arrange
        var state = new EntregueState();
        var os = new OrdemServico("OS-001", "123.456.789-00", "Modelo", "NS123", "Defeito");

        // Act
        Action act = () => state.Cancelar(os, "Motivo");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Uma OS entregue não pode ser cancelada.");
    }

    [Fact]
    public void ObterStatusNome_DeveRetornarEntregue()
    {
        // Arrange
        var state = new EntregueState();

        // Act
        var result = state.ObterStatusNome();

        // Assert
        result.Should().Be("Entregue");
    }
}
