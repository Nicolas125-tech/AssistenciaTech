using System;
using Xunit;
using AssistenciaTech.Domain.Entities;
using AssistenciaTech.Domain.States;
using FluentAssertions;

namespace AssistenciaTech.Domain.Tests;

public class ProntoStateTests
{
    [Fact]
    public void Avancar_DeveMudarParaEntregueState_EDefinirDataSaida()
    {
        // Arrange
        var os = new OrdemServico("OS-001", "123.456.789-00", "Modelo", "NS123", "Defeito");
        os.DefinirEstado(new ProntoState());

        // Act
        os.AvancarStatus();

        // Assert
        os.EstadoAtual.Should().BeOfType<EntregueState>();
        os.DataSaida.Should().NotBeNull();
    }

    [Fact]
    public void Cancelar_DeveLancarExcecao()
    {
        // Arrange
        var state = new ProntoState();
        var os = new OrdemServico("OS-001", "123.456.789-00", "Modelo", "NS123", "Defeito");

        // Act
        Action act = () => state.Cancelar(os, "Motivo");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Equipamentos prontos não podem ser cancelados. Mova para Entregue.");
    }

    [Fact]
    public void ObterStatusNome_DeveRetornarProntoParaRetirada()
    {
        // Arrange
        var state = new ProntoState();

        // Act
        var result = state.ObterStatusNome();

        // Assert
        result.Should().Be("Pronto para Retirada");
    }
}
