using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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
            if (TempData["ErroBanco"] != null)
            {
                ViewBag.ErroBanco = TempData["ErroBanco"].ToString();
            }
            try
            {
                // Busca todas as OS com os dados dos Clientes
                var todasOS = await _context.OrdensServico.Include(o => o.Cliente).ToListAsync();

                // Dashboard Data (Verificação segura contra nulos)
                ViewBag.TotalAbertas = todasOS?.Count(o => o.Status != "Pronto" && o.Status != "Entregue") ?? 0;
                ViewBag.EquipamentosProntos = todasOS?.Count(o => o.Status == "Pronto") ?? 0;
                ViewBag.FaturamentoPrevisto = todasOS?.Where(o => o.Status != "Entregue" && o.Status != "Cancelado").Sum(o => o.ValorOrcamento) ?? 0m;

                // Retorna as ordens ordenadas da mais recente para a mais antiga
                var ordensOrdenadas = todasOS?.OrderByDescending(o => o.DataEntrada).ToList() ?? new List<OrdemServico>();
                return View(ordensOrdenadas);
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB_CONNECTION_ERROR (Admin/Index): " + ex.ToString());
                // Log the exception in a real scenario
                ViewBag.ErroBanco = "Erro ao conectar ao banco de dados. Por favor, tente novamente mais tarde.";
                
                // Zera os valores do Dashboard
                ViewBag.TotalAbertas = 0;
                ViewBag.EquipamentosProntos = 0;
                ViewBag.FaturamentoPrevisto = 0m;

                // Retorna uma lista vazia para a View não quebrar
                return View(new List<OrdemServico>());
            }
        }

        // GET: Admin/Create
        public IActionResult Create()
        {
            try
            {
                // Popula o dropdown de clientes
                ViewBag.Clientes = new SelectList(_context.Clientes.Select(c => new { Id = c.Id, Descricao = c.Nome + " - CPF: " + c.Cpf + " - Tel: " + c.Telefone }), "Id", "Descricao");
                return View();
            }
            catch (Exception)
            {
                TempData["ErroBanco"] = "Não foi possível carregar a tela de criação. O banco de dados está inacessível.";
                return RedirectToAction(nameof(Index));
            }
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
            catch (Exception)
            {
                // Em um ambiente real, faríamos um log (ex: Serilog)
                ModelState.AddModelError(string.Empty, "Erro ao salvar a Ordem de Serviço. Verifique os dados e tente novamente.");
            }

            // Se falhou, retorna os dados para o formulário
            ViewBag.Clientes = new SelectList(_context.Clientes.Select(c => new { Id = c.Id, Descricao = c.Nome + " - CPF: " + c.Cpf + " - Tel: " + c.Telefone }), "Id", "Descricao", ordemServico.ClienteId);
            return View(ordemServico);
        }

        // GET: Admin/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var ordemServico = await _context.OrdensServico
                                                 .Include(o => o.Cliente)
                                                 .FirstOrDefaultAsync(m => m.Id == id);
                
                if (ordemServico == null) return NotFound();

                return View(ordemServico);
            }
            catch (Exception)
            {
                TempData["ErroBanco"] = "Não foi possível carregar a tela de edição. O banco de dados está inacessível.";
                return RedirectToAction(nameof(Index));
            }
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
            catch (Exception)
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
            catch (Exception)
            {
                // Tratar caso a OS tenha dependências impeditivas (raro neste escopo)
                return RedirectToAction(nameof(Index), new { erro = "Não foi possível excluir a OS." });
            }
        }

        // GET: Admin/ImprimirOs/5
        [HttpGet]
        public async Task<IActionResult> ImprimirOs(int id)
        {
            var os = await _context.OrdensServico.Include(o => o.Cliente).FirstOrDefaultAsync(o => o.Id == id);
            
            if (os == null) return NotFound();

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().Element(ComposeFooter);

                    // Componentes do PDF extraídos em métodos locais (ou Action) para limpeza do código
                    void ComposeHeader(IContainer headerContainer)
                    {
                        headerContainer.Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text("Assistência Tech").FontSize(24).SemiBold().FontColor(Colors.Blue.Darken2);
                                column.Item().Text("Soluções em Tecnologia");
                            });
                            
                            row.ConstantItem(150).AlignRight().Text($"Ordem de Serviço Nº {os.Id}").FontSize(16).Bold();
                        });
                    }

                    void ComposeContent(IContainer contentContainer)
                    {
                        contentContainer.PaddingVertical(1, Unit.Centimetre).Column(column =>
                        {
                            column.Spacing(20);

                            // Dados do Cliente
                            column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c =>
                            {
                                c.Item().Text("Dados do Cliente").SemiBold().FontSize(14);
                                c.Item().Text($"Nome: {os.Cliente?.Nome}");
                                c.Item().Text($"CPF: {os.Cliente?.Cpf}");
                            });

                            // Dados do Equipamento e Serviço
                            column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c =>
                            {
                                c.Item().Text("Detalhes do Serviço").SemiBold().FontSize(14);
                                c.Item().Text($"Equipamento: {os.Equipamento}");
                                c.Item().Text($"Defeito Relatado: {os.ProblemaRelatado}");
                                c.Item().Text($"Status: {os.Status}").FontColor(os.Status == "Pronto" || os.Status == "Entregue" ? Colors.Green.Darken2 : Colors.Orange.Darken2).SemiBold();
                                c.Item().Text($"Data de Entrada: {os.DataEntrada:dd/MM/yyyy HH:mm}");
                            });
                        });
                    }

                    void ComposeFooter(IContainer footerContainer)
                    {
                        footerContainer.Column(column =>
                        {
                            // Orçamento
                            column.Item().PaddingBottom(2, Unit.Centimetre).AlignRight()
                                .Text($"Valor do Orçamento: {os.ValorOrcamento:C}").FontSize(16).SemiBold();

                            // Assinatura
                            column.Item().AlignCenter().Text("___________________________________________________");
                            column.Item().AlignCenter().Text("Assinatura do Cliente");
                        });
                    }
                });
            }).GeneratePdf();

            return File(pdfBytes, "application/pdf", $"OS_{os.Id}_{os.Cliente?.Nome}.pdf");
        }
    }
}
