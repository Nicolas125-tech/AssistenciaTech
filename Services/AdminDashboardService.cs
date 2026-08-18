using AssistenciaTech.Data;
using AssistenciaTech.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
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
        public int TotalOrdens { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }

    public interface IAdminDashboardService
    {
        Task<DashboardDto> GetDashboardDataAsync(string searchString, string statusFilter, int page = 1, int pageSize = 50);
    }

    public class StatusGroupDto
    {
        public string? Status { get; set; }
        public int Count { get; set; }
        public decimal TotalValor { get; set; }
    }

    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private const string CacheKeyStatusGroup = "AdminDashboard_StatusGroup";
        private const string CacheKeyTotalOrdens = "AdminDashboard_TotalOrdens";

        public AdminDashboardService(AppDbContext context, IMemoryCache cache = null)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<DashboardDto> GetDashboardDataAsync(string searchString, string statusFilter, int page = 1, int pageSize = 50)
        {
            var query = _context.OrdensServico.Include(o => o.Cliente).AsNoTracking().AsQueryable();

            bool hasFilters = !string.IsNullOrEmpty(searchString) || !string.IsNullOrEmpty(statusFilter);

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(o => (o.Cliente.Nome != null && o.Cliente.Nome.Contains(searchString)) || o.Equipamento.Contains(searchString) || o.Id.ToString() == searchString);
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(o => o.Status == statusFilter);
            }

            List<StatusGroupDto>? statusGroupDb = null;
            int totalOrdens;

            if (!hasFilters && _cache != null)
            {
                if (!_cache.TryGetValue(CacheKeyStatusGroup, out statusGroupDb) || statusGroupDb == null)
                {
                    var ordens = await _context.OrdensServico
                        .Select(o => new { o.Status, o.ValorOrcamento })
                        .ToListAsync();

                    statusGroupDb = ordens
                        .GroupBy(o => o.Status)
                        .Select(g => new StatusGroupDto
                        {
                            Status = g.Key,
                            Count = g.Count(),
                            TotalValor = g.Sum(x => x.ValorOrcamento)
                        })
                        .ToList();

                    _cache.Set(CacheKeyStatusGroup, statusGroupDb, CacheDuration);
                }

                if (!_cache.TryGetValue(CacheKeyTotalOrdens, out totalOrdens))
                {
                    totalOrdens = await _context.OrdensServico.CountAsync();
                    _cache.Set(CacheKeyTotalOrdens, totalOrdens, CacheDuration);
                }
            }
            else
            {
                var ordens = await query
                    .Select(o => new { o.Status, o.ValorOrcamento })
                    .ToListAsync();

                statusGroupDb = ordens.GroupBy(o => o.Status)
                    .Select(g => new StatusGroupDto
                    {
                        Status = g.Key,
                        Count = g.Count(),
                        TotalValor = g.Sum(x => x.ValorOrcamento)
                    })
                    .ToList();

                totalOrdens = ordens.Count;
            }

            int totalPages = (int)Math.Ceiling(totalOrdens / (double)pageSize);

            var ordensOrdenadas = await query
                .OrderByDescending(o => o.DataEntrada)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            int totalAbertas = 0;
            int equipamentosProntos = 0;
            decimal faturamentoPrevisto = 0;

            var count = statusGroupDb?.Count ?? 0;
            var chartLabels = new List<string>(count);
            var chartData = new List<int>(count);

            if (statusGroupDb != null)
            {
                foreach (var g in statusGroupDb)
                {
                    if (g.Status != WorkflowStatus.Concluido && g.Status != WorkflowStatus.Entregue)
                        totalAbertas += g.Count;

                    if (g.Status == WorkflowStatus.Concluido)
                        equipamentosProntos += g.Count;

                    if (g.Status != WorkflowStatus.Entregue && g.Status != "Cancelado")
                        faturamentoPrevisto += g.TotalValor;

                    chartLabels.Add(g.Status!);
                    chartData.Add(g.Count);
                }
            }

            return new DashboardDto
            {
                Ordens = ordensOrdenadas,
                ChartLabels = chartLabels,
                ChartData = chartData,
                TotalAbertas = totalAbertas,
                EquipamentosProntos = equipamentosProntos,
                FaturamentoPrevisto = faturamentoPrevisto,
                TotalOrdens = totalOrdens,
                CurrentPage = page,
                TotalPages = totalPages
            };
        }
    }
}
