using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.DTOs;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace AssistenciaTech.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class ClientesController : Controller
    {
        private readonly AppDbContext _context;

        public ClientesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Clientes
        public async Task<IActionResult> Index()
        {
            return View(await _context.Clientes.AsNoTracking().ToListAsync());
        }

        // GET: Clientes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Clientes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClienteCreateDto clienteDto)
        {
            if (ModelState.IsValid)
            {
                var cliente = new Cliente
                {
                    Nome = clienteDto.Nome,
                    Cpf = clienteDto.Cpf,
                    Telefone = clienteDto.Telefone,
                    Email = clienteDto.Email,
                    TelegramChatId = clienteDto.TelegramChatId
                };
                _context.Add(cliente);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(clienteDto);
        }

        // GET: Clientes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound();

            var clienteDto = new ClienteUpdateDto
            {
                Id = cliente.Id,
                Nome = cliente.Nome,
                Cpf = cliente.Cpf,
                Telefone = cliente.Telefone,
                Email = cliente.Email,
                TelegramChatId = cliente.TelegramChatId
            };

            return View(clienteDto);
        }

        // POST: Clientes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ClienteUpdateDto clienteDto)
        {
            if (id != clienteDto.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var clienteExistente = await _context.Clientes.FindAsync(id);
                    if (clienteExistente == null) return NotFound();

                    clienteExistente.Nome = clienteDto.Nome;
                    clienteExistente.Cpf = clienteDto.Cpf;
                    clienteExistente.Telefone = clienteDto.Telefone;
                    clienteExistente.Email = clienteDto.Email;
                    clienteExistente.TelegramChatId = clienteDto.TelegramChatId;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ClienteExists(clienteDto.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(clienteDto);
        }

        // POST: Clientes/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente != null)
            {
                // Verifica se o cliente tem OS associadas
                var temOS = await _context.OrdensServico.AnyAsync(o => o.ClienteId == id);
                if (temOS)
                {
                    // Pode tratar como preferir, ex: retornar erro
                    return RedirectToAction(nameof(Index), new { erro = "Não é possível excluir um cliente com Ordens de Serviço associadas." });
                }

                _context.Clientes.Remove(cliente);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ClienteExists(int id)
        {
            return _context.Clientes.Any(e => e.Id == id);
        }
    }
}
