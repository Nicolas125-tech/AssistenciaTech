using System.Threading.Tasks;

namespace AssistenciaTech.Services
{
    public interface IEquipamentoBackupService
    {
        Task ProcessarTrocaEquipamentoAsync(int? equipamentoAntigoId, int? equipamentoNovoId);
    }
}
