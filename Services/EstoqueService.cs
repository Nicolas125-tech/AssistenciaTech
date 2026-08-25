using AssistenciaTech.Data;
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

        public EstoqueService(AppDbContext context)
        {
            _context = context;
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
            return true;
        }

        /// <summary>
        /// Retorna peças cujo estoque atual é menor ou igual à quantidade mínima configurada.
        /// </summary>
        public async Task<List<AssistenciaTech.Models.Peca>> ObterAlertasDeEstoqueAsync()
        {
            return await _context.Pecas
                .Where(p => p.QuantidadeMinima > 0 && p.QuantidadeEstoque <= p.QuantidadeMinima)
                .OrderBy(p => p.QuantidadeEstoque)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
