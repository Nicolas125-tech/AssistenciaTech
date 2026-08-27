using AssistenciaTech.Data;
using AssistenciaTech.Extensions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AssistenciaTech.Services
{
    public interface IEstoqueService
    {
        Task<bool> DeduzirEstoque(int ordemServicoId);
        Task<bool> RestaurarEstoque(int ordemServicoId);
        Task<List<AssistenciaTech.Models.Peca>> ObterAlertasDeEstoqueAsync();
    }

    public class EstoqueService : IEstoqueService
    {
        private readonly AppDbContext _context;
        private readonly IResilientCacheService _cache;
        private const string CacheKeyAlertasEstoque = "Estoque_AlertasEstoque";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

        public EstoqueService(AppDbContext context, IResilientCacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<bool> DeduzirEstoque(int ordemServicoId)
        {
            var agrupado = await _context.OrdemServicoPecas
                .Where(op => op.OrdemServicoId == ordemServicoId)
                .GroupBy(op => op.PecaId)
                .Select(g => new { PecaId = g.Key, QuantidadeTotal = g.Sum(x => x.Quantidade) })
                .ToListAsync();

            if (!agrupado.Any()) return true; // Nada a deduzir

            var pecaIds = agrupado.Select(a => a.PecaId).ToList();
            var pecas = await _context.Pecas.Where(p => pecaIds.Contains(p.Id)).ToListAsync();
            var quantidadesDict = agrupado.ToDictionary(a => a.PecaId, a => a.QuantidadeTotal);

            foreach (var peca in pecas)
            {
                if (quantidadesDict.TryGetValue(peca.Id, out var qtdSolicitada))
                {
                    // Regra: Impede estoque de ficar negativo
                    if (peca.QuantidadeEstoque < qtdSolicitada)
                    {
                        throw new InvalidOperationException($"Estoque insuficiente para a peça: {peca.Nome}. Disponível: {peca.QuantidadeEstoque}, Solicitado: {qtdSolicitada}");
                    }

                    peca.QuantidadeEstoque -= qtdSolicitada;
                }
            }

            await _context.SaveChangesAsync();
            await _cache.RemoveAsync(CacheKeyAlertasEstoque);
            return true;
        }

        public async Task<bool> RestaurarEstoque(int ordemServicoId)
        {
            var agrupado = await _context.OrdemServicoPecas
                .Where(op => op.OrdemServicoId == ordemServicoId)
                .GroupBy(op => op.PecaId)
                .Select(g => new { PecaId = g.Key, QuantidadeTotal = g.Sum(x => x.Quantidade) })
                .ToListAsync();

            if (!agrupado.Any()) return true;

            var pecaIds = agrupado.Select(a => a.PecaId).ToList();
            var pecas = await _context.Pecas.Where(p => pecaIds.Contains(p.Id)).ToListAsync();
            var quantidadesDict = agrupado.ToDictionary(a => a.PecaId, a => a.QuantidadeTotal);

            foreach (var peca in pecas)
            {
                if (quantidadesDict.TryGetValue(peca.Id, out var qtdRestaurar))
                {
                    peca.QuantidadeEstoque += qtdRestaurar;
                }
            }

            await _context.SaveChangesAsync();
            await _cache.RemoveAsync(CacheKeyAlertasEstoque);
            return true;
        }

        /// <summary>
        /// Retorna peças cujo estoque atual é menor ou igual à quantidade mínima configurada.
        /// </summary>

        public async Task<List<AssistenciaTech.Models.Peca>> ObterAlertasDeEstoqueAsync()
        {
            var cached = await _cache.GetAsync<List<AssistenciaTech.Models.Peca>>(CacheKeyAlertasEstoque);
            if (cached != null)
            {
                return cached;
            }

            var result = await _context.Pecas
                .Where(p => p.QuantidadeMinima > 0 && p.QuantidadeEstoque <= p.QuantidadeMinima)
                .OrderBy(p => p.QuantidadeEstoque)
                .AsNoTracking()
                .ToListAsync();

            await _cache.SetAsync(CacheKeyAlertasEstoque, result, CacheDuration);

            return result;
        }

    }
}
