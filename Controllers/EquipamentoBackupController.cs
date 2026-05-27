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
    public class EquipamentoBackupController : Controller
    {
        private readonly AppDbContext _context;

        public EquipamentoBackupController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.EquipamentosBackup.ToListAsync());
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Descricao,NumeroSerie,Disponivel")] EquipamentoBackup equipamento)
        {
            if (ModelState.IsValid)
            {
                _context.Add(equipamento);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(equipamento);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var equipamento = await _context.EquipamentosBackup.FindAsync(id);
            if (equipamento == null) return NotFound();
            return View(equipamento);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Descricao,NumeroSerie,Disponivel")] EquipamentoBackup equipamento)
        {
            if (id != equipamento.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(equipamento);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EquipamentoExists(equipamento.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(equipamento);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var equipamento = await _context.EquipamentosBackup.FindAsync(id);
            if (equipamento != null)
            {
                _context.EquipamentosBackup.Remove(equipamento);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // Ação rápida para devolver equipamento
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Devolver(int id)
        {
            var equipamento = await _context.EquipamentosBackup.FindAsync(id);
            if (equipamento != null)
            {
                equipamento.Disponivel = true;
                _context.Update(equipamento);
                await _context.SaveChangesAsync();
            }
            // Retorna para a página anterior, pode ser a OS ou o próprio index
            string referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
            {
                return Redirect(referer);
            }
            return RedirectToAction(nameof(Index));
        }

        private bool EquipamentoExists(int id)
        {
            return _context.EquipamentosBackup.Any(e => e.Id == id);
        }
    }
}
