using System;
using Xunit;
using AssistenciaTech.Domain.Entities;
using AssistenciaTech.Domain.States;

namespace AssistenciaTech.Domain.Tests.States;

public class RecebidoStateTests
{
    private OrdemServico CreateValidOrdemServico()
    {
        return new OrdemServico(
            numeroOS: "OS-001",
            clientCpf: "123.456.789-00",
            equipamentoModelo: "Dell XPS",
            numeroSerie: "SN12345",
            defeitoRelatado: "Não liga"
        );
    }

    [Fact]
    public void Avancar_DeveMudarEstadoParaEmAnalise()
    {
        // Arrange
        var os = CreateValidOrdemServico();
        var estadoInicial = os.EstadoAtual;

        // Act
        os.AvancarStatus();

        // Assert
        Assert.IsType<RecebidoState>(estadoInicial); // Ensures it starts at RecebidoState
        Assert.IsType<EmAnaliseState>(os.EstadoAtual);
    }

    [Fact]
    public void Cancelar_DeveMudarEstadoParaCancelado_EDefinirMotivo()
    {
        // Arrange
        var os = CreateValidOrdemServico();
        var motivo = "Cliente desistiu";
        var estadoInicial = os.EstadoAtual;

        // Act
        os.CancelarOS(motivo);

        // Assert
        Assert.IsType<RecebidoState>(estadoInicial); // Ensures it starts at RecebidoState
        Assert.IsType<CanceladoState>(os.EstadoAtual);
        Assert.Equal(motivo, os.MotivoCancelamento);
    }
}
