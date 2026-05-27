using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using Microsoft.AspNetCore.Authorization;

namespace AssistenciaTech.Controllers
{
    [Authorize]
    public class TecnicosController : Controller
    {
        private readonly AppDbContext _context;

        public TecnicosController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Tecnicos.ToListAsync());
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Nome,PercentualComissao,Ativo")] Tecnico tecnico)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tecnico);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(tecnico);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var tecnico = await _context.Tecnicos.FindAsync(id);
            if (tecnico == null) return NotFound();
            return View(tecnico);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nome,PercentualComissao,Ativo")] Tecnico tecnico)
        {
            if (id != tecnico.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tecnico);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TecnicoExists(tecnico.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(tecnico);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tecnico = await _context.Tecnicos.FindAsync(id);
            if (tecnico != null)
            {
                _context.Tecnicos.Remove(tecnico);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool TecnicoExists(int id)
        {
            return _context.Tecnicos.Any(e => e.Id == id);
        }
    }
}
