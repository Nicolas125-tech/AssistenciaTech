using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
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
        public async Task<IActionResult> Create([Bind("Nome,QuantidadeEstoque,ValorUnitario")] Peca peca)
        {
            if (ModelState.IsValid)
            {
                _context.Add(peca);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(peca);
        }

        // GET: Pecas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var peca = await _context.Pecas.FindAsync(id);
            if (peca == null) return NotFound();

            return View(peca);
        }

        // POST: Pecas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nome,QuantidadeEstoque,ValorUnitario")] Peca peca)
        {
            if (id != peca.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var pecaExistente = await _context.Pecas.FindAsync(id);
                    if (pecaExistente == null) return NotFound();

                    pecaExistente.Nome = peca.Nome;
                    pecaExistente.QuantidadeEstoque = peca.QuantidadeEstoque;
                    pecaExistente.ValorUnitario = peca.ValorUnitario;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PecaExists(peca.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(peca);
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
