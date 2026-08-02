using System;
using AssistenciaTech.Domain.Entities;
using AssistenciaTech.Domain.States;
using Xunit;

namespace AssistenciaTech.Domain.Tests.States;

public class OrdemServicoStatesTests
{
    private OrdemServico CriarOrdemServico()
    {
        return new OrdemServico("OS-001", "123.456.789-00", "Notebook Dell", "SN123456", "Não liga");
    }

    [Fact]
    public void RecebidoState_Avancar_DeveMudarParaEmAnaliseState()
    {
        var os = CriarOrdemServico();
        var estadoInicial = new RecebidoState();
        os.DefinirEstado(estadoInicial);

        estadoInicial.Avancar(os);

        Assert.IsType<EmAnaliseState>(os.EstadoAtual);
    }

    [Fact]
    public void RecebidoState_Cancelar_DeveMudarParaCanceladoState_E_DefinirMotivo()
    {
        var os = CriarOrdemServico();
        var estadoInicial = new RecebidoState();
        os.DefinirEstado(estadoInicial);
        var motivo = "Cliente desistiu";

        estadoInicial.Cancelar(os, motivo);

        Assert.IsType<CanceladoState>(os.EstadoAtual);
        Assert.Equal(motivo, os.MotivoCancelamento);
    }

    [Fact]
    public void RecebidoState_ObterStatusNome_DeveRetornarRecebido()
    {
        var estado = new RecebidoState();
        Assert.Equal("Recebido", estado.ObterStatusNome());
    }

    [Fact]
    public void EmAnaliseState_Avancar_SemDiagnostico_DeveLancarExcecao()
    {
        var os = CriarOrdemServico();
        var estado = new EmAnaliseState();
        os.DefinirEstado(estado);
        // DiagnosticoTecnico is null initially

        var exception = Assert.Throws<InvalidOperationException>(() => estado.Avancar(os));
        Assert.Equal("Não é possível aprovar ou finalizar uma OS sem o diagnóstico técnico.", exception.Message);
    }

    [Fact]
    public void EmAnaliseState_Avancar_ComDiagnostico_DeveMudarParaProntoState()
    {
        var os = CriarOrdemServico();
        var estado = new EmAnaliseState();
        os.DefinirEstado(estado);
        os.DefinirDiagnostico("Placa mãe queimada", 1500m);

        estado.Avancar(os);

        Assert.IsType<ProntoState>(os.EstadoAtual);
    }

    [Fact]
    public void EmAnaliseState_Cancelar_DeveMudarParaCanceladoState_E_DefinirMotivo()
    {
        var os = CriarOrdemServico();
        var estado = new EmAnaliseState();
        os.DefinirEstado(estado);
        var motivo = "Falta de peça";

        estado.Cancelar(os, motivo);

        Assert.IsType<CanceladoState>(os.EstadoAtual);
        Assert.Equal(motivo, os.MotivoCancelamento);
    }

    [Fact]
    public void EmAnaliseState_ObterStatusNome_DeveRetornarEmAnalise()
    {
        var estado = new EmAnaliseState();
        Assert.Equal("Em Análise", estado.ObterStatusNome());
    }

    [Fact]
    public void ProntoState_Avancar_DeveDefinirDataSaida_E_MudarParaEntregueState()
    {
        var os = CriarOrdemServico();
        var estado = new ProntoState();
        os.DefinirEstado(estado);

        estado.Avancar(os);

        Assert.IsType<EntregueState>(os.EstadoAtual);
        Assert.NotNull(os.DataSaida);
    }

    [Fact]
    public void ProntoState_Cancelar_DeveLancarExcecao()
    {
        var os = CriarOrdemServico();
        var estado = new ProntoState();
        os.DefinirEstado(estado);

        var exception = Assert.Throws<InvalidOperationException>(() => estado.Cancelar(os, "motivo qualquer"));
        Assert.Equal("Equipamentos prontos não podem ser cancelados. Mova para Entregue.", exception.Message);
    }

    [Fact]
    public void ProntoState_ObterStatusNome_DeveRetornarProntoParaRetirada()
    {
        var estado = new ProntoState();
        Assert.Equal("Pronto para Retirada", estado.ObterStatusNome());
    }

    [Fact]
    public void EntregueState_Avancar_DeveLancarExcecao()
    {
        var os = CriarOrdemServico();
        var estado = new EntregueState();
        os.DefinirEstado(estado);

        var exception = Assert.Throws<InvalidOperationException>(() => estado.Avancar(os));
        Assert.Equal("A ordem de serviço já foi finalizada e entregue.", exception.Message);
    }

    [Fact]
    public void EntregueState_Cancelar_DeveLancarExcecao()
    {
        var os = CriarOrdemServico();
        var estado = new EntregueState();
        os.DefinirEstado(estado);

        var exception = Assert.Throws<InvalidOperationException>(() => estado.Cancelar(os, "motivo qualquer"));
        Assert.Equal("Uma OS entregue não pode ser cancelada.", exception.Message);
    }

    [Fact]
    public void EntregueState_ObterStatusNome_DeveRetornarEntregue()
    {
        var estado = new EntregueState();
        Assert.Equal("Entregue", estado.ObterStatusNome());
    }

    [Fact]
    public void CanceladoState_Avancar_DeveLancarExcecao()
    {
        var os = CriarOrdemServico();
        var estado = new CanceladoState();
        os.DefinirEstado(estado);

        var exception = Assert.Throws<InvalidOperationException>(() => estado.Avancar(os));
        Assert.Equal("Uma ordem de serviço cancelada não pode avançar.", exception.Message);
    }

    [Fact]
    public void CanceladoState_Cancelar_DeveLancarExcecao()
    {
        var os = CriarOrdemServico();
        var estado = new CanceladoState();
        os.DefinirEstado(estado);

        var exception = Assert.Throws<InvalidOperationException>(() => estado.Cancelar(os, "motivo qualquer"));
        Assert.Equal("A ordem de serviço já está cancelada.", exception.Message);
    }

    [Fact]
    public void CanceladoState_ObterStatusNome_DeveRetornarCancelado()
    {
        var estado = new CanceladoState();
        Assert.Equal("Cancelado", estado.ObterStatusNome());
    }
}
