using System;
using AssistenciaTech.Domain.Entities;
using AssistenciaTech.Domain.States;
using Xunit;

namespace AssistenciaTech.Domain.Tests.Entities;

public class OrdemServicoTests
{
    private OrdemServico CriarOrdemServicoPadrao()
    {
        return new OrdemServico("OS-001", "123.456.789-00", "Notebook Dell", "SN123456", "Não liga");
    }

    [Fact]
    public void OrdemServico_Construtor_DeveInicializarPropriedadesCorretamente()
    {
        var numeroOS = "OS-001";
        var clientCpf = "123.456.789-00";
        var equipamentoModelo = "Notebook Dell";
        var numeroSerie = "SN123456";
        var defeitoRelatado = "Não liga";

        var os = new OrdemServico(numeroOS, clientCpf, equipamentoModelo, numeroSerie, defeitoRelatado);

        Assert.NotEqual(Guid.Empty, os.Id);
        Assert.Equal(numeroOS, os.NumeroOS);
        Assert.Equal(clientCpf, os.ClientCpf);
        Assert.Equal(equipamentoModelo, os.EquipamentoModelo);
        Assert.Equal(numeroSerie, os.NumeroSerie);
        Assert.Equal(defeitoRelatado, os.DefeitoRelatado);
        Assert.True((DateTime.UtcNow - os.DataEntrada).TotalSeconds < 5); // Verifica se foi inicializado recentemente
        Assert.IsType<RecebidoState>(os.EstadoAtual);
    }

    [Fact]
    public void OrdemServico_DefinirEstado_DeveAtualizarEstadoAtual()
    {
        var os = CriarOrdemServicoPadrao();
        var novoEstado = new EmAnaliseState();

        os.DefinirEstado(novoEstado);

        Assert.IsType<EmAnaliseState>(os.EstadoAtual);
    }

    [Fact]
    public void OrdemServico_DefinirDiagnostico_ComDiagnosticoValido_DeveAtualizarPropriedades()
    {
        var os = CriarOrdemServicoPadrao();
        var diagnostico = "Placa mãe queimada";
        var orcamento = 1500m;

        os.DefinirDiagnostico(diagnostico, orcamento);

        Assert.Equal(diagnostico, os.DiagnosticoTecnico);
        Assert.Equal(orcamento, os.ValorOrcamento);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void OrdemServico_DefinirDiagnostico_ComDiagnosticoInvalido_DeveLancarExcecao(string? diagnosticoInvalido)
    {
        var os = CriarOrdemServicoPadrao();

        var exception = Assert.Throws<ArgumentException>(() => os.DefinirDiagnostico(diagnosticoInvalido!, 100m));
        Assert.Equal("O diagnóstico técnico não pode ser vazio.", exception.Message);
    }

    [Fact]
    public void OrdemServico_DefinirDataSaida_DeveAtualizarDataSaida()
    {
        var os = CriarOrdemServicoPadrao();

        os.DefinirDataSaida();

        Assert.NotNull(os.DataSaida);
        Assert.True((DateTime.UtcNow - os.DataSaida.Value).TotalSeconds < 5);
    }

    [Fact]
    public void OrdemServico_DefinirCancelamento_DeveAtualizarMotivoCancelamento()
    {
        var os = CriarOrdemServicoPadrao();
        var motivo = "Cliente desistiu";

        os.DefinirCancelamento(motivo);

        Assert.Equal(motivo, os.MotivoCancelamento);
    }

    [Fact]
    public void OrdemServico_AvancarStatus_DeveDelegarParaEstadoAtual()
    {
        var os = CriarOrdemServicoPadrao();
        // Estado inicial é RecebidoState, que ao Avancar vai para EmAnaliseState

        os.AvancarStatus();

        Assert.IsType<EmAnaliseState>(os.EstadoAtual);
    }

    [Fact]
    public void OrdemServico_CancelarOS_DeveDelegarParaEstadoAtual()
    {
        var os = CriarOrdemServicoPadrao();
        var motivo = "Cliente desistiu";
        // Estado inicial é RecebidoState, que ao Cancelar vai para CanceladoState

        os.CancelarOS(motivo);

        Assert.IsType<CanceladoState>(os.EstadoAtual);
        Assert.Equal(motivo, os.MotivoCancelamento);
    }
}
