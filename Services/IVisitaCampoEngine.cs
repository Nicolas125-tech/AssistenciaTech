using System.Collections.Generic;
using System.Threading.Tasks;
using AssistenciaTech.DTOs;

namespace AssistenciaTech.Services
{
    public interface IVisitaCampoEngine
    {
        Task<MobileAuthResult> AutenticarTecnicoAsync(string username, string password);
        Task<VisitaResult> RegistrarCheckInAsync(int osId, int tecnicoId, decimal latitude, decimal longitude);
        Task<VisitaResult> FinalizarVisitaAsync(int osId, int visitaId, int tecnicoId, string assinaturaBase64, string laudoFinal, string? tecnicoNome);
        Task<VisitaSyncResult> SincronizarVisitasOfflineAsync(int tecnicoId, IEnumerable<VisitaCampoSyncDto> visitas);
    }
}
