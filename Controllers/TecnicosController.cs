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
        public async Task<IActionResult> Create(TecnicoCreateDto tecnicoDto)
        {
            if (ModelState.IsValid)
            {
                var tecnico = new Tecnico
                {
                    Nome = tecnicoDto.Nome,
                    PercentualComissao = tecnicoDto.PercentualComissao,
                    Ativo = tecnicoDto.Ativo
                };

                _context.Add(tecnico);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(tecnicoDto);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var tecnico = await _context.Tecnicos.FindAsync(id);
            if (tecnico == null) return NotFound();

            var tecnicoDto = new TecnicoUpdateDto
            {
                Id = tecnico.Id,
                Nome = tecnico.Nome,
                PercentualComissao = tecnico.PercentualComissao,
                Ativo = tecnico.Ativo
            };

            return View(tecnicoDto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TecnicoUpdateDto tecnicoDto)
        {
            if (id != tecnicoDto.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var tecnico = await _context.Tecnicos.FindAsync(id);
                    if (tecnico == null) return NotFound();

                    tecnico.Nome = tecnicoDto.Nome;
                    tecnico.PercentualComissao = tecnicoDto.PercentualComissao;
                    tecnico.Ativo = tecnicoDto.Ativo;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TecnicoExists(tecnicoDto.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(tecnicoDto);
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
