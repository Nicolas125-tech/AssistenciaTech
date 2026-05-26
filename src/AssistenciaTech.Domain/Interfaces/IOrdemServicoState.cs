namespace AssistenciaTech.Domain.Interfaces;

using AssistenciaTech.Domain.Entities;

public interface IOrdemServicoState
{
    void Avancar(OrdemServico os);
    void Cancelar(OrdemServico os, string motivo);
    string ObterStatusNome();
}
