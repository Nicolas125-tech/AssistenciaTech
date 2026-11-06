using System.Collections.Generic;
using System.Threading.Tasks;
using AssistenciaTech.DTOs;
using AssistenciaTech.Models;

namespace AssistenciaTech.Services
{
    public interface IEstoqueEngine
    {
        // Consultas
        Task<List<Peca>> ObterTodasAsync();
        Task<Peca?> ObterPorIdAsync(int id);
        Task<List<Peca>> ObterAlertasDeEstoqueAsync();

        // Mutações de Catálogo
        Task<EstoqueResult> CriarPecaAsync(PecaCreateDto dto);
        Task<EstoqueResult> AtualizarPecaAsync(PecaEditDto dto);
        Task<EstoqueResult> ExcluirPecaAsync(int id);

        // Ciclo de Vida da Ordem de Serviço
        Task<EstoqueResult> DeduzirEstoqueAsync(int ordemServicoId);
        Task<EstoqueResult> RestaurarEstoqueAsync(int ordemServicoId);
    }
}
