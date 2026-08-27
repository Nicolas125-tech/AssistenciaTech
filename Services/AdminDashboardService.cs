using AssistenciaTech.Data;
using AssistenciaTech.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using AssistenciaTech.Extensions;
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
        public List<AssistenciaTech.Models.Peca> AlertasEstoque { get; set; } = new();
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
        private readonly IDistributedCache? _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private const string CacheKeyStatusGroup = "AdminDashboard_StatusGroup";
        private const string CacheKeyTotalOrdens = "AdminDashboard_TotalOrdens";

        private readonly IEstoqueService _estoqueService;

        public AdminDashboardService(AppDbContext context, IEstoqueService estoqueService, IDistributedCache? cache = null)
        {
            _context = context;
            _estoqueService = estoqueService;
            _cache = cache;
        }

        public async Task<DashboardDto> GetDashboardDataAsync(string searchString, string statusFilter, int page = 1, int pageSize = 50)
        {
            var query = ApplyFilters(searchString, statusFilter);

            bool hasFilters = !string.IsNullOrEmpty(searchString) || !string.IsNullOrEmpty(statusFilter);

            var (statusGroupDb, totalOrdens) = await GetStatusGroupDataAsync(query, hasFilters);

            int totalPages = (int)Math.Ceiling(totalOrdens / (double)pageSize);

            var ordensOrdenadas = await GetPagedOrdersAsync(query, page, pageSize);

            var metrics = CalculateMetrics(statusGroupDb);

            var alertasEstoque = await _estoqueService.ObterAlertasDeEstoqueAsync();

            return new DashboardDto
            {
                Ordens = ordensOrdenadas,
                ChartLabels = metrics.ChartLabels,
                ChartData = metrics.ChartData,
                TotalAbertas = metrics.TotalAbertas,
                EquipamentosProntos = metrics.EquipamentosProntos,
                FaturamentoPrevisto = metrics.FaturamentoPrevisto,
                TotalOrdens = totalOrdens,
                CurrentPage = page,
                TotalPages = totalPages,
                AlertasEstoque = alertasEstoque
            };
        }

        private IQueryable<OrdemServico> ApplyFilters(string searchString, string statusFilter)
        {
            var query = _context.OrdensServico.Include(o => o.Cliente).AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(o => (o.Cliente.Nome != null && o.Cliente.Nome.Contains(searchString)) || o.Equipamento.Contains(searchString) || o.Id.ToString() == searchString);
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(o => o.Status == statusFilter);
            }

            return query;
        }

        private async Task<(List<StatusGroupDto>? StatusGroupDb, int TotalOrdens)> GetStatusGroupDataAsync(IQueryable<OrdemServico> query, bool hasFilters)
        {
            List<StatusGroupDto>? statusGroupDb = null;
            int totalOrdens;


            if (!hasFilters && _cache != null)
            {
                try
                {
                    statusGroupDb = await _cache.GetRecordAsync<List<StatusGroupDto>>(CacheKeyStatusGroup);
                }
                catch
                {
                    // Redis indisponível — ignora e segue para o banco
                    statusGroupDb = null;
                }

                if (statusGroupDb == null)
                {
                    statusGroupDb = await _context.OrdensServico
                        .GroupBy(o => o.Status)
                        .Select(g => new StatusGroupDto
                        {
                            Status = g.Key,
                            Count = g.Count(),
                            TotalValor = g.Sum(x => x.ValorOrcamento)
                        })
                        .ToListAsync();

                    try
                    {
                        await _cache.SetRecordAsync(CacheKeyStatusGroup, statusGroupDb, absoluteExpireTime: CacheDuration);
                    }
                    catch
                    {
                        // Falha ao gravar no Redis — segue sem cache
                    }
                }

                int? cachedTotalOrdens = null;
                try
                {
                    cachedTotalOrdens = await _cache.GetRecordAsync<int?>(CacheKeyTotalOrdens);
                }
                catch
                {
                    // Redis indisponível
                }

                if (cachedTotalOrdens == null)
                {
                    totalOrdens = await _context.OrdensServico.CountAsync();
                    try
                    {
                        await _cache.SetRecordAsync(CacheKeyTotalOrdens, (int?)totalOrdens, absoluteExpireTime: CacheDuration);
                    }
                    catch
                    {
                        // Falha ao gravar no Redis
                    }
                }
                else
                {
                    totalOrdens = cachedTotalOrdens.Value;
                }
            }

            else
            {
                statusGroupDb = await query
                    .GroupBy(o => o.Status)
                    .Select(g => new StatusGroupDto
                    {
                        Status = g.Key,
                        Count = g.Count(),
                        TotalValor = g.Sum(x => x.ValorOrcamento)
                    })
                    .ToListAsync();

                totalOrdens = await query.CountAsync();
            }

            return (statusGroupDb, totalOrdens);
        }

        private async Task<List<OrdemServico>> GetPagedOrdersAsync(IQueryable<OrdemServico> query, int page, int pageSize)
        {
            return await query
                .OrderByDescending(o => o.DataEntrada)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        private static (List<string> ChartLabels, List<int> ChartData, int TotalAbertas, int EquipamentosProntos, decimal FaturamentoPrevisto) CalculateMetrics(List<StatusGroupDto>? statusGroupDb)
        {
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

            return (chartLabels, chartData, totalAbertas, equipamentosProntos, faturamentoPrevisto);
        }
    }
}
