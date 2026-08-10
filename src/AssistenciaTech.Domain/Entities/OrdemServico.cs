using System;
using AssistenciaTech.Domain.Interfaces;
using AssistenciaTech.Domain.States;

namespace AssistenciaTech.Domain.Entities;

public class OrdemServico
{
    public Guid Id { get; private set; }
    public string NumeroOS { get; private set; }
    public string ClientCpf { get; private set; }
    public string EquipamentoModelo { get; private set; }
    public string NumeroSerie { get; private set; }
    public string DefeitoRelatado { get; private set; }
    public string? DiagnosticoTecnico { get; private set; }
    public decimal ValorOrcamento { get; private set; }
    public DateTime DataEntrada { get; private set; }
    public DateTime? DataSaida { get; private set; }
    public string? MotivoCancelamento { get; private set; }

    public IOrdemServicoState EstadoAtual { get; private set; }

    public OrdemServico(string numeroOS, string clientCpf, string equipamentoModelo, string numeroSerie, string defeitoRelatado)
    {
        Id = Guid.NewGuid();
        NumeroOS = numeroOS;
        ClientCpf = clientCpf;
        EquipamentoModelo = equipamentoModelo;
        NumeroSerie = numeroSerie;
        DefeitoRelatado = defeitoRelatado;
        DataEntrada = DateTime.UtcNow;
        EstadoAtual = new RecebidoState();
    }

    public void DefinirEstado(IOrdemServicoState novoEstado)
    {
        EstadoAtual = novoEstado;
    }

    public void DefinirDiagnostico(string diagnostico, decimal orcamento)
    {
        if (string.IsNullOrWhiteSpace(diagnostico))
            throw new ArgumentException("O diagnóstico técnico não pode ser vazio.");

        DiagnosticoTecnico = diagnostico;
        ValorOrcamento = orcamento;
    }

    public void DefinirDataSaida()
    {
        DataSaida = DateTime.UtcNow;
    }

    public void DefinirCancelamento(string motivo)
    {
        MotivoCancelamento = motivo;
    }

    public void AvancarStatus() => EstadoAtual.Avancar(this);
    public void CancelarOS(string motivo) => EstadoAtual.Cancelar(this, motivo);
}
