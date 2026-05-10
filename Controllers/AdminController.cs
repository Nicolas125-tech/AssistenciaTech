using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace AssistenciaTech.Controllers
{
    /// <summary>
    /// Controller responsável pelo Painel Administrativo (CRUD de Ordens de Serviço).
    /// </summary>
    [Authorize]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Index
        public async Task<IActionResult> Index()
        {
            // Busca todas as OS com os dados dos Clientes
            var todasOS = await _context.OrdensServico.Include(o => o.Cliente).ToListAsync();

            // Dashboard Data
            ViewBag.TotalAbertas = todasOS.Count(o => o.Status != "Pronto" && o.Status != "Entregue");
            ViewBag.EquipamentosProntos = todasOS.Count(o => o.Status == "Pronto");
            ViewBag.FaturamentoPrevisto = todasOS.Where(o => o.Status != "Entregue" && o.Status != "Cancelado").Sum(o => o.ValorOrcamento);

            // Retorna as ordens ordenadas da mais recente para a mais antiga
            var ordensOrdenadas = todasOS.OrderByDescending(o => o.DataEntrada).ToList();
            return View(ordensOrdenadas);
        }

        // GET: Admin/Create
        public IActionResult Create()
        {
            // Popula o dropdown de clientes
            ViewBag.Clientes = new SelectList(_context.Clientes, "Id", "Nome");
            return View();
        }

        // POST: Admin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrdemServico ordemServico)
        {
            try
            {
                // Validação de segurança básica e ModelState
                if (ModelState.IsValid)
                {
                    ordemServico.DataEntrada = DateTime.Now;
                    _context.Add(ordemServico);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                // Em um ambiente real, faríamos um log (ex: Serilog)
                ModelState.AddModelError(string.Empty, "Erro ao salvar a Ordem de Serviço. Verifique os dados e tente novamente.");
            }

            // Se falhou, retorna os dados para o formulário
            ViewBag.Clientes = new SelectList(_context.Clientes, "Id", "Nome", ordemServico.ClienteId);
            return View(ordemServico);
        }

        // GET: Admin/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var ordemServico = await _context.OrdensServico
                                             .Include(o => o.Cliente)
                                             .FirstOrDefaultAsync(m => m.Id == id);
            
            if (ordemServico == null) return NotFound();

            return View(ordemServico);
        }

        // POST: Admin/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OrdemServico ordemServico)
        {
            if (id != ordemServico.Id) return NotFound();

            try
            {
                // Busca a OS existente no banco
                var ordemExistente = await _context.OrdensServico.FindAsync(id);
                if (ordemExistente == null) return NotFound();

                // Atualiza apenas os campos permitidos
                ordemExistente.Equipamento = ordemServico.Equipamento;
                ordemExistente.ProblemaRelatado = ordemServico.ProblemaRelatado;
                ordemExistente.Status = ordemServico.Status;
                ordemExistente.ValorOrcamento = ordemServico.ValorOrcamento;

                // Regra de negócio: Se o status for alterado para 'Entregue', seta a Data de Saída
                if (ordemExistente.Status == "Entregue" && ordemExistente.DataSaida == null)
                {
                    ordemExistente.DataSaida = DateTime.Now;
                }
                else if (ordemExistente.Status != "Entregue")
                {
                    ordemExistente.DataSaida = null; // Caso o status retroceda
                }

                _context.Update(ordemExistente);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Ocorreu um erro ao atualizar os dados.");
                return View(ordemServico);
            }
        }

        // POST: Admin/Delete/5
        // Neste formato não teremos uma View dedicada para o Delete,
        // o post virá direto da Index por um botão de confirmação.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var ordemServico = await _context.OrdensServico.FindAsync(id);
                if (ordemServico != null)
                {
                    _context.OrdensServico.Remove(ordemServico);
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Tratar caso a OS tenha dependências impeditivas (raro neste escopo)
                return RedirectToAction(nameof(Index), new { erro = "Não foi possível excluir a OS." });
            }
        }
    }
}
