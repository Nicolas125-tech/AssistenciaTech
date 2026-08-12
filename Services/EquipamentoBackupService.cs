using System.Threading.Tasks;
using AssistenciaTech.Data;

namespace AssistenciaTech.Services
{
    public class EquipamentoBackupService : IEquipamentoBackupService
    {
        private readonly AppDbContext _context;

        public EquipamentoBackupService(AppDbContext context)
        {
            _context = context;
        }

        public async Task ProcessarTrocaEquipamentoAsync(int? equipamentoAntigoId, int? equipamentoNovoId)
        {
            if (equipamentoAntigoId == equipamentoNovoId)
                return;

            // Se ele tinha um antes e tirou, marcamos o antigo como disponivel
            if (equipamentoAntigoId is int antigoId)
            {
                var backupAntigo = await _context.EquipamentosBackup.FindAsync(antigoId);
                if (backupAntigo != null) backupAntigo.Disponivel = true;
            }

            // Se ele atrelou um novo, marcamos como indisponível
            if (equipamentoNovoId is int novoId)
            {
                var backupNovo = await _context.EquipamentosBackup.FindAsync(novoId);
                if (backupNovo != null) backupNovo.Disponivel = false;
            }
        }
    }
}
