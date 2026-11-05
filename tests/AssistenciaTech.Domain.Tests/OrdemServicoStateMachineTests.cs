using System;
using AssistenciaTech.Domain.States;
using FluentAssertions;
using Xunit;

namespace AssistenciaTech.Domain.Tests;

public class OrdemServicoStateMachineTests
{
    [Theory]
    [InlineData("Recebido", typeof(RecebidoState))]
    [InlineData("Em Análise", typeof(EmAnaliseState))]
    [InlineData("Aguardando Aprovação do Orçamento", typeof(AguardandoAprovacaoState))]
    [InlineData("Aguardando Peças", typeof(AguardandoPecasState))]
    [InlineData("Em Reparo", typeof(EmReparoState))]
    [InlineData("Concluído", typeof(ProntoState))]
    [InlineData("Pronto para Retirada", typeof(ProntoState))]
    [InlineData("Entregue ao Cliente", typeof(EntregueState))]
    [InlineData("Entregue", typeof(EntregueState))]
    [InlineData("Cancelado", typeof(CanceladoState))]
    public void ResolverEstado_DeveRetornarImplementacaoCorretaDoPadraoState(string status, Type tipoEsperado)
    {
        var estado = OrdemServicoStateMachine.ResolverEstado(status);

        estado.Should().BeOfType(tipoEsperado);
    }

    [Fact]
    public void ValidarTransicao_ParaEntregueComBackupPendente_DeveRetornarFalsoComMensagemDeDominio()
    {
        var valido = OrdemServicoStateMachine.ValidarTransicao(
            statusAtual: "Concluído",
            novoStatus: "Entregue ao Cliente",
            possuiBackupNaoDevolvido: true,
            nomeEquipamentoBackup: "Notebook Reserva #01",
            out var erro);

        valido.Should().BeFalse();
        erro.Should().Contain("Notebook Reserva #01");
    }

    [Fact]
    public void ValidarTransicao_ParaEntregueSemBackupPendente_DeveRetornarVerdadeiro()
    {
        var valido = OrdemServicoStateMachine.ValidarTransicao(
            statusAtual: "Concluído",
            novoStatus: "Entregue ao Cliente",
            possuiBackupNaoDevolvido: false,
            nomeEquipamentoBackup: null,
            out var erro);

        valido.Should().BeTrue();
        erro.Should().BeNull();
    }

    [Fact]
    public void ExigeBaixaDeEstoque_QuandoTransicionaParaConcluidoOuEntregue_DeveRetornarVerdadeiro()
    {
        OrdemServicoStateMachine.ExigeBaixaDeEstoque("Em Reparo", "Concluído", dataConclusaoExistente: null)
            .Should().BeTrue();

        OrdemServicoStateMachine.ExigeEstornoDeEstoque("Concluído", "Em Reparo")
            .Should().BeTrue();
    }
}
