using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.DTOs;
using AssistenciaTech.Extensions;
using AssistenciaTech.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssistenciaTech.Services
{
    public class EstoqueEngine : IEstoqueEngine
    {
        private readonly AppDbContext _context;
        private readonly IResilientCacheService _cache;
        private readonly ILogger<EstoqueEngine> _logger;

        private const string CacheKeyAlertasEstoque = "Estoque_AlertasEstoque";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

        public EstoqueEngine(AppDbContext context, IResilientCacheService cache, ILogger<EstoqueEngine> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        private async Task InvalidateCacheAsync()
        {
            try
            {
                await _cache.RemoveAsync(CacheKeyAlertasEstoque);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao invalidar cache de alertas de estoque.");
            }
        }

        public async Task<List<Peca>> ObterTodasAsync()
        {
            return await _context.Pecas
                .AsNoTracking()
                .OrderBy(p => p.Nome)
                .ToListAsync();
        }

        public async Task<Peca?> ObterPorIdAsync(int id)
        {
            return await _context.Pecas.FindAsync(id);
        }

        public async Task<List<Peca>> ObterAlertasDeEstoqueAsync()
        {
            var cached = await _cache.GetAsync<List<Peca>>(CacheKeyAlertasEstoque);
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

        public async Task<EstoqueResult> CriarPecaAsync(PecaCreateDto dto)
        {
            if (dto == null)
            {
                return EstoqueResult.Falha("Dados da peça não fornecidos.");
            }

            if (string.IsNullOrWhiteSpace(dto.Nome))
            {
                return EstoqueResult.Falha("O nome da peça é obrigatório.");
            }

            var peca = new Peca
            {
                Nome = dto.Nome.Trim(),
                QuantidadeEstoque = dto.QuantidadeEstoque,
                QuantidadeMinima = dto.QuantidadeMinima,
                ValorUnitario = dto.ValorUnitario
            };

            _context.Pecas.Add(peca);
            await _context.SaveChangesAsync();
            await InvalidateCacheAsync();

            return EstoqueResult.Ok(peca, "Peça cadastrada com sucesso.");
        }

        public async Task<EstoqueResult> AtualizarPecaAsync(PecaEditDto dto)
        {
            if (dto == null)
            {
                return EstoqueResult.Falha("Dados da peça não fornecidos.");
            }

            var pecaExistente = await _context.Pecas.FindAsync(dto.Id);
            if (pecaExistente == null)
            {
                return EstoqueResult.Falha($"Peça com ID {dto.Id} não encontrada.");
            }

            pecaExistente.Nome = dto.Nome?.Trim() ?? string.Empty;
            pecaExistente.QuantidadeEstoque = dto.QuantidadeEstoque;
            pecaExistente.QuantidadeMinima = dto.QuantidadeMinima;
            pecaExistente.ValorUnitario = dto.ValorUnitario;

            try
            {
                await _context.SaveChangesAsync();
                await InvalidateCacheAsync();
                return EstoqueResult.Ok(pecaExistente, "Peça atualizada com sucesso.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Conflito de concorrência ao atualizar peça {PecaId}", dto.Id);
                return EstoqueResult.Falha("A peça foi modificada simultaneamente por outro usuário. Recarregue a página e tente novamente.");
            }
        }

        public async Task<EstoqueResult> ExcluirPecaAsync(int id)
        {
            var peca = await _context.Pecas.FindAsync(id);
            if (peca == null)
            {
                return EstoqueResult.Falha("Peça não encontrada.");
            }

            bool possuiVinculoOS = await _context.OrdemServicoPecas.AnyAsync(op => op.PecaId == id);
            if (possuiVinculoOS)
            {
                return EstoqueResult.Falha("Não é possível excluir a peça porque ela já foi utilizada em Ordens de Serviço históricas.");
            }

            _context.Pecas.Remove(peca);
            await _context.SaveChangesAsync();
            await InvalidateCacheAsync();

            return EstoqueResult.Ok(peca, "Peça excluída com sucesso.");
        }

        public async Task<EstoqueResult> DeduzirEstoqueAsync(int ordemServicoId)
        {
            var agrupado = await _context.OrdemServicoPecas
                .Where(op => op.OrdemServicoId == ordemServicoId)
                .GroupBy(op => op.PecaId)
                .Select(g => new { PecaId = g.Key, QuantidadeTotal = g.Sum(x => x.Quantidade) })
                .ToListAsync();

            if (!agrupado.Any())
            {
                return EstoqueResult.Ok(null, "Nenhuma peça associada à Ordem de Serviço.");
            }

            var pecas = await _context.OrdemServicoPecas
                .Where(op => op.OrdemServicoId == ordemServicoId)
                .Select(op => op.Peca!)
                .Distinct()
                .ToListAsync();

            var quantidadesDict = agrupado.ToDictionary(a => a.PecaId, a => a.QuantidadeTotal);

            // Valida suficiência antes de alterar saldos
            foreach (var peca in pecas)
            {
                if (quantidadesDict.TryGetValue(peca.Id, out var qtdSolicitada))
                {
                    if (peca.QuantidadeEstoque < qtdSolicitada)
                    {
                        return EstoqueResult.Falha($"Estoque insuficiente para a peça: {peca.Nome}. Disponível: {peca.QuantidadeEstoque}, Solicitado: {qtdSolicitada}");
                    }
                }
            }

            foreach (var peca in pecas)
            {
                if (quantidadesDict.TryGetValue(peca.Id, out var qtdSolicitada))
                {
                    peca.QuantidadeEstoque -= qtdSolicitada;
                }
            }

            await _context.SaveChangesAsync();
            await InvalidateCacheAsync();
            return EstoqueResult.Ok(null, "Estoque deduzido com sucesso.");
        }

        public async Task<EstoqueResult> RestaurarEstoqueAsync(int ordemServicoId)
        {
            var agrupado = await _context.OrdemServicoPecas
                .Where(op => op.OrdemServicoId == ordemServicoId)
                .GroupBy(op => op.PecaId)
                .Select(g => new { PecaId = g.Key, QuantidadeTotal = g.Sum(x => x.Quantidade) })
                .ToListAsync();

            if (!agrupado.Any())
            {
                return EstoqueResult.Ok(null, "Nenhuma peça associada à Ordem de Serviço.");
            }

            var pecas = await _context.OrdemServicoPecas
                .Where(op => op.OrdemServicoId == ordemServicoId)
                .Select(op => op.Peca!)
                .Distinct()
                .ToListAsync();

            var quantidadesDict = agrupado.ToDictionary(a => a.PecaId, a => a.QuantidadeTotal);

            foreach (var peca in pecas)
            {
                if (quantidadesDict.TryGetValue(peca.Id, out var qtdRestaurar))
                {
                    peca.QuantidadeEstoque += qtdRestaurar;
                }
            }

            await _context.SaveChangesAsync();
            await InvalidateCacheAsync();
            return EstoqueResult.Ok(null, "Estoque restaurado com sucesso.");
        }
    }
}
