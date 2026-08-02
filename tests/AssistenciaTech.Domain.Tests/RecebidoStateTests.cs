using System;
using Xunit;
using AssistenciaTech.Domain.Entities;
using AssistenciaTech.Domain.States;
using FluentAssertions;

namespace AssistenciaTech.Domain.Tests;

public class RecebidoStateTests
{
    [Fact]
    public void Avancar_DeveMudarParaEmAnaliseState()
    {
        // Arrange
        var os = new OrdemServico("OS-001", "123.456.789-00", "Modelo", "NS123", "Defeito");
        // O estado inicial padrão é RecebidoState, mas vamos garantir:
        os.DefinirEstado(new RecebidoState());

        // Act
        os.AvancarStatus();

        // Assert
        os.EstadoAtual.Should().BeOfType<EmAnaliseState>();
    }

    [Fact]
    public void Cancelar_DeveMudarParaCanceladoState_EDefinirMotivo()
    {
        // Arrange
        var os = new OrdemServico("OS-001", "123.456.789-00", "Modelo", "NS123", "Defeito");
        os.DefinirEstado(new RecebidoState());
        var motivo = "Cliente desistiu";

        // Act
        os.CancelarOS(motivo);

        // Assert
        os.EstadoAtual.Should().BeOfType<CanceladoState>();
        os.MotivoCancelamento.Should().Be(motivo);
    }
}
