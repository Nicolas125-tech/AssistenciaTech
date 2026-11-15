using System.Collections.Generic;
using System.IO;
using System.Threading;
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

        Task<List<SelecaoItemDto>> ObterClientesParaSelecaoAsync();
        Task<OrdemEdicaoDto?> ObterDadosEdicaoAsync(int id);
        Task<OrdemEdicaoLookupsDto> ObterLookupsEdicaoAsync(int clienteId, int? equipamentoBackupId);
        Task<OrdemServicoResult> ExcluirOrdemAsync(int id, string? usuarioResponsavel = null);
        Task<OrdemServico?> ObterOrdemParaImpressaoAsync(int id);
        Task ExportarOrdensCsvAsync(Stream destino, CancellationToken ct = default);
    }
}
