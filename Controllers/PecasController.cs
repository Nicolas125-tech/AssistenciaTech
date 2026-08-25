using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace AssistenciaTech.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class PecasController : Controller
    {
        private readonly AppDbContext _context;

        public PecasController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Pecas
        public async Task<IActionResult> Index()
        {
            return View(await _context.Pecas.AsNoTracking().ToListAsync());
        }

        // GET: Pecas/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Pecas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PecaCreateDto dto)
        {
            if (ModelState.IsValid)
            {
                var peca = new Peca
                {
                    Nome = dto.Nome,
                    QuantidadeEstoque = dto.QuantidadeEstoque,
                    QuantidadeMinima = dto.QuantidadeMinima,
                    ValorUnitario = dto.ValorUnitario
                };
                _context.Add(peca);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(dto);
        }

        // GET: Pecas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var peca = await _context.Pecas.FindAsync(id);
            if (peca == null) return NotFound();

            var dto = new PecaEditDto
            {
                Id = peca.Id,
                Nome = peca.Nome,
                QuantidadeEstoque = peca.QuantidadeEstoque,
                QuantidadeMinima = peca.QuantidadeMinima,
                ValorUnitario = peca.ValorUnitario
            };

            return View(dto);
        }

        // POST: Pecas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PecaEditDto dto)
        {
            if (id != dto.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var pecaExistente = await _context.Pecas.FindAsync(id);
                    if (pecaExistente == null) return NotFound();

                    pecaExistente.Nome = dto.Nome;
                    pecaExistente.QuantidadeEstoque = dto.QuantidadeEstoque;
                    pecaExistente.QuantidadeMinima = dto.QuantidadeMinima;
                    pecaExistente.ValorUnitario = dto.ValorUnitario;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PecaExists(dto.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(dto);
        }

        // POST: Pecas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var peca = await _context.Pecas.FindAsync(id);
            if (peca != null)
            {
                _context.Pecas.Remove(peca);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool PecaExists(int id)
        {
            return _context.Pecas.Any(e => e.Id == id);
        }
    }
}
