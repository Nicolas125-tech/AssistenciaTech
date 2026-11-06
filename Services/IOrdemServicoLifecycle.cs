using System.Threading.Tasks;
using AssistenciaTech.DTOs;
using AssistenciaTech.Models;
using Microsoft.AspNetCore.Http;

namespace AssistenciaTech.Services
{
    public interface IOrdemServicoLifecycle
    {
        Task<OrdemServicoResult> RegistrarEntradaAsync(OrdemServicoCreateDto dto, string? usuarioResponsavel = null);
        Task<OrdemServicoResult> AtualizarOrdemAsync(int osId, OrdemServico ordemAtualizada, IFormFileCollection? fotos = null, string? usuarioResponsavel = null);
        Task<OrdemServicoResult> AvancarStatusAsync(int osId, string novoStatus, string? laudoTecnico = null, string? usuarioResponsavel = null);
    }
}
