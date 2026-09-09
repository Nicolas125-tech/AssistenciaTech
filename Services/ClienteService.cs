using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AssistenciaTech.Services
{
    public class ClienteService : IClienteService
    {
        private readonly AppDbContext _context;

        public ClienteService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<SelectListItem>> GetClientesSelectListAsync(int? selectedId = null)
        {
            var clientes = await _context.Clientes
                .AsNoTracking()
                .Select(c => new
                {
                    Id = c.Id,
                    Descricao = $"{c.Nome} - CPF: {c.Cpf} - Tel: {c.Telefone}"
                })
                .ToListAsync();

            var selectList = clientes.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Descricao,
                Selected = selectedId.HasValue && c.Id == selectedId.Value
            }).ToList();

            return selectList;
        }
    }
}
