using System;
using AssistenciaTech.Domain.Entities;
using AssistenciaTech.Domain.Interfaces;

namespace AssistenciaTech.Domain.States;

public class AguardandoAprovacaoState : IOrdemServicoState
{
    public void Avancar(OrdemServico os) => os.DefinirEstado(new EmReparoState());

    public void Cancelar(OrdemServico os, string motivo)
    {
        os.DefinirCancelamento(motivo);
        os.DefinirEstado(new CanceladoState());
    }

    public string ObterStatusNome() => "Aguardando Aprovação do Orçamento";
}

public class AguardandoPecasState : IOrdemServicoState
{
    public void Avancar(OrdemServico os) => os.DefinirEstado(new EmReparoState());

    public void Cancelar(OrdemServico os, string motivo)
    {
        os.DefinirCancelamento(motivo);
        os.DefinirEstado(new CanceladoState());
    }

    public string ObterStatusNome() => "Aguardando Peças";
}

public class EmReparoState : IOrdemServicoState
{
    public void Avancar(OrdemServico os) => os.DefinirEstado(new ProntoState());

    public void Cancelar(OrdemServico os, string motivo)
    {
        os.DefinirCancelamento(motivo);
        os.DefinirEstado(new CanceladoState());
    }

    public string ObterStatusNome() => "Em Reparo";
}

/// <summary>
/// Máquina de estados unificada de domínio para validação de transições e ciclo de vida da Ordem de Serviço.
/// Conecta o State Pattern de domínio ao modelo persistido no EF Core.
/// </summary>
public static class OrdemServicoStateMachine
{
    public const string Recebido = "Recebido";
    public const string EmAnalise = "Em Análise";
    public const string AguardandoAprovacao = "Aguardando Aprovação do Orçamento";
    public const string AguardandoPecas = "Aguardando Peças";
    public const string EmReparo = "Em Reparo";
    public const string Concluido = "Concluído";
    public const string ProntoParaRetirada = "Pronto para Retirada";
    public const string Entregue = "Entregue ao Cliente";
    public const string EntregueCurto = "Entregue";
    public const string Cancelado = "Cancelado";

    public static IOrdemServicoState ResolverEstado(string? status)
    {
        return status switch
        {
            Recebido => new RecebidoState(),
            EmAnalise => new EmAnaliseState(),
            AguardandoAprovacao or "Aguardando Aprovação" => new AguardandoAprovacaoState(),
            AguardandoPecas => new AguardandoPecasState(),
            EmReparo => new EmReparoState(),
            Concluido or ProntoParaRetirada => new ProntoState(),
            Entregue or EntregueCurto => new EntregueState(),
            Cancelado => new CanceladoState(),
            _ => new RecebidoState()
        };
    }

    public static bool ValidarTransicao(
        string? statusAtual,
        string? novoStatus,
        bool possuiBackupNaoDevolvido,
        string? nomeEquipamentoBackup,
        out string? mensagemErro)
    {
        mensagemErro = null;

        if ((novoStatus == Entregue || novoStatus == EntregueCurto) && possuiBackupNaoDevolvido)
        {
            var descricao = string.IsNullOrWhiteSpace(nomeEquipamentoBackup) ? "Backup" : nomeEquipamentoBackup;
            mensagemErro = $"O status não pode ser 'Entregue' até que o equipamento '{descricao}' seja devolvido no sistema.";
            return false;
        }

        return true;
    }

    public static bool ExigeBaixaDeEstoque(string? statusAnterior, string? novoStatus, DateTime? dataConclusaoExistente)
    {
        bool ehStatusFinal = novoStatus == Concluido || novoStatus == Entregue || novoStatus == EntregueCurto;
        return ehStatusFinal && dataConclusaoExistente == null;
    }

    public static bool ExigeEstornoDeEstoque(string? statusAnterior, string? novoStatus)
    {
        bool eraConcluido = statusAnterior == Concluido;
        bool deixouDeSerFinal = novoStatus != Concluido && novoStatus != Entregue && novoStatus != EntregueCurto;
        return eraConcluido && deixouDeSerFinal;
    }
}
