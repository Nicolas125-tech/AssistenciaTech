using System.Collections.Generic;
using System.Threading.Tasks;
using AssistenciaTech.Models;

namespace AssistenciaTech.Services
{
    public interface IAreaClienteEngine
    {
        // Consulta Pública (Deslogada)
        Task<ConsultaPublicaResult> ConsultarStatusPublicoAsync(int numeroOS, string? cpf);

        // Área Logada do Cliente
        Task<List<OrdemServico>> ObterEquipamentosPorEmailAsync(string? email);
        Task<VisualizacaoOSResult> ObterDetalhesSegurosAsync(int id, string? email);

        // Autenticação Web do Cliente
        Task<ClienteAuthResult> AutenticarClientePorEmailAsync(string? email);
    }
}
