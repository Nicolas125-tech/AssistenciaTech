using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AssistenciaTech.DTOs;
using AssistenciaTech.Services;
using Microsoft.AspNetCore.Authorization;

namespace AssistenciaTech.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class PecasController : Controller
    {
        private readonly IEstoqueEngine _estoqueEngine;

        public PecasController(IEstoqueEngine estoqueEngine)
        {
            _estoqueEngine = estoqueEngine;
        }

        // GET: Pecas
        public async Task<IActionResult> Index()
        {
            var pecas = await _estoqueEngine.ObterTodasAsync();
            return View(pecas);
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
                var result = await _estoqueEngine.CriarPecaAsync(dto);
                if (result.Sucesso)
                {
                    TempData["MensagemSucesso"] = result.Mensagem;
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError(string.Empty, result.Mensagem);
            }
            return View(dto);
        }

        // GET: Pecas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var peca = await _estoqueEngine.ObterPorIdAsync(id.Value);
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
                var result = await _estoqueEngine.AtualizarPecaAsync(dto);
                if (result.Sucesso)
                {
                    TempData["MensagemSucesso"] = result.Mensagem;
                    return RedirectToAction(nameof(Index));
                }

                if (result.Mensagem.Contains("não encontrada", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound();
                }

                ModelState.AddModelError(string.Empty, result.Mensagem);
            }
            return View(dto);
        }

        // POST: Pecas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var result = await _estoqueEngine.ExcluirPecaAsync(id);
            if (!result.Sucesso)
            {
                TempData["MensagemErro"] = result.Mensagem;
            }
            else
            {
                TempData["MensagemSucesso"] = result.Mensagem;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
