using AssistenciaTech.Domain.Entities;
using AssistenciaTech.Domain.Interfaces;

namespace AssistenciaTech.Domain.States;

public class RecebidoState : IOrdemServicoState
{
    public void Avancar(OrdemServico os)
    {
        os.DefinirEstado(new EmAnaliseState());
    }

    public void Cancelar(OrdemServico os, string motivo)
    {
        os.DefinirCancelamento(motivo);
        os.DefinirEstado(new CanceladoState());
    }

    public string ObterStatusNome() => "Recebido";
}

public class EmAnaliseState : IOrdemServicoState
{
    public void Avancar(OrdemServico os)
    {
        if (string.IsNullOrWhiteSpace(os.DiagnosticoTecnico))
            throw new InvalidOperationException("Não é possível aprovar ou finalizar uma OS sem o diagnóstico técnico.");

        os.DefinirEstado(new ProntoState());
    }

    public void Cancelar(OrdemServico os, string motivo)
    {
        os.DefinirCancelamento(motivo);
        os.DefinirEstado(new CanceladoState());
    }

    public string ObterStatusNome() => "Em Análise";
}

public class ProntoState : IOrdemServicoState
{
    public void Avancar(OrdemServico os)
    {
        os.DefinirDataSaida();
        os.DefinirEstado(new EntregueState());
    }

    public void Cancelar(OrdemServico os, string motivo)
    {
        throw new InvalidOperationException("Equipamentos prontos não podem ser cancelados. Mova para Entregue.");
    }

    public string ObterStatusNome() => "Pronto para Retirada";
}

public class EntregueState : IOrdemServicoState
{
    public void Avancar(OrdemServico os) => throw new InvalidOperationException("A ordem de serviço já foi finalizada e entregue.");
    public void Cancelar(OrdemServico os, string motivo) => throw new InvalidOperationException("Uma OS entregue não pode ser cancelada.");
    public string ObterStatusNome() => "Entregue";
}

public class CanceladoState : IOrdemServicoState
{
    public void Avancar(OrdemServico os) => throw new InvalidOperationException("Uma ordem de serviço cancelada não pode avançar.");
    public void Cancelar(OrdemServico os, string motivo) => throw new InvalidOperationException("A ordem de serviço já está cancelada.");
    public string ObterStatusNome() => "Cancelado";
}
