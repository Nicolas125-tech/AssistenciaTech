using AssistenciaTech.Data;
using AssistenciaTech.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AssistenciaTech.Services
{
    public class DashboardDto
    {
        public List<OrdemServico> Ordens { get; set; } = new();
        public List<string> ChartLabels { get; set; } = new();
        public List<int> ChartData { get; set; } = new();
        public int TotalAbertas { get; set; }
        public int EquipamentosProntos { get; set; }
        public decimal FaturamentoPrevisto { get; set; }
    }

    public interface IAdminDashboardService
    {
        Task<DashboardDto> GetDashboardDataAsync(string searchString, string statusFilter);
    }

    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly AppDbContext _context;

        public AdminDashboardService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardDto> GetDashboardDataAsync(string searchString, string statusFilter)
        {
            var query = _context.OrdensServico.Include(o => o.Cliente).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(o => (o.Cliente.Nome != null && o.Cliente.Nome.Contains(searchString)) || o.Equipamento.Contains(searchString) || o.Id.ToString() == searchString);
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(o => o.Status == statusFilter);
            }

            // Executa a agregação no banco de dados para evitar o carregamento de todos os registros na memória
            var statusGroupDb = await query.GroupBy(o => o.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count(),
                    TotalValor = g.Sum(x => x.ValorOrcamento)
                })
                .ToListAsync();

            var ordensOrdenadas = await query
                .OrderByDescending(o => o.DataEntrada)
                .ToListAsync();

            int totalAbertas = 0;
            int equipamentosProntos = 0;
            decimal faturamentoPrevisto = 0;

            foreach (var g in statusGroupDb)
            {
                if (g.Status != WorkflowStatus.Concluido && g.Status != WorkflowStatus.Entregue)
                    totalAbertas += g.Count;

                if (g.Status == WorkflowStatus.Concluido)
                    equipamentosProntos += g.Count;

                if (g.Status != WorkflowStatus.Entregue && g.Status != "Cancelado")
                    faturamentoPrevisto += g.TotalValor;
            }

            return new DashboardDto
            {
                Ordens = ordensOrdenadas,
                ChartLabels = statusGroupDb.Select(g => g.Status).ToList(),
                ChartData = statusGroupDb.Select(g => g.Count).ToList(),
                TotalAbertas = totalAbertas,
                EquipamentosProntos = equipamentosProntos,
                FaturamentoPrevisto = faturamentoPrevisto
            };
        }
    }
}
