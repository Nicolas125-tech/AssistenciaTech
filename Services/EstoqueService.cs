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
            var pecasUtilizadas = await _context.OrdemServicoPecas
                .Include(op => op.Peca)
                .Where(op => op.OrdemServicoId == ordemServicoId)
                .ToListAsync();

            if (!pecasUtilizadas.Any()) return true; // Nada a deduzir

            foreach (var item in pecasUtilizadas)
            {
                if (item.Peca != null)
                {
                    // Regra: Impede estoque de ficar negativo (conforme aprovação implícita)
                    if (item.Peca.QuantidadeEstoque < item.Quantidade)
                    {
                        throw new InvalidOperationException($"Estoque insuficiente para a peça: {item.Peca.Nome}. Disponível: {item.Peca.QuantidadeEstoque}, Solicitado: {item.Quantidade}");
                    }

                    item.Peca.QuantidadeEstoque -= item.Quantidade;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RestaurarEstoque(int ordemServicoId)
        {
            var pecasUtilizadas = await _context.OrdemServicoPecas
                .Include(op => op.Peca)
                .Where(op => op.OrdemServicoId == ordemServicoId)
                .ToListAsync();

            if (!pecasUtilizadas.Any()) return true;

            foreach (var item in pecasUtilizadas)
            {
                if (item.Peca != null)
                {
                    item.Peca.QuantidadeEstoque += item.Quantidade;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
