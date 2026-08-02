using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace AssistenciaTech.Controllers
{
    /// <summary>
    /// Controller responsável pelo Painel Administrativo (CRUD de Ordens de Serviço).
    /// </summary>
    [Authorize]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEstoqueService _estoqueService;
        private readonly IWebHostEnvironment _env;

        public AdminController(AppDbContext context, IEstoqueService estoqueService, IWebHostEnvironment env)
        {
            _context = context;
            _estoqueService = estoqueService;
            _env = env;
        }

        // GET: Admin/Index
        [HttpGet]
        public async Task<IActionResult> ExportarCsv()
        {
            var todasOS = await _context.OrdensServico.Include(o => o.Cliente).OrderByDescending(o => o.Id).ToListAsync();
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Id,Cliente,Equipamento,Data Entrada,Status,Valor Orçamento");
            foreach (var os in todasOS)
            {
                csv.AppendLine($"{os.Id},\"{os.Cliente?.Nome}\",\"{os.Equipamento}\",{os.DataEntrada:dd/MM/yyyy},{os.Status},{os.ValorOrcamento}");
            }
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "OrdensDeServico.csv");
        }

        public async Task<IActionResult> Index(string searchString, string statusFilter)
        {
            if (TempData["ErroBanco"] != null)
            {
                ViewBag.ErroBanco = TempData["ErroBanco"].ToString();
            }
            try
            {
                // Busca todas as OS com os dados dos Clientes
                var query = _context.OrdensServico.Include(o => o.Cliente).AsQueryable();

                if (!string.IsNullOrEmpty(searchString))
                {
                    query = query.Where(o => (o.Cliente.Nome != null && o.Cliente.Nome.Contains(searchString)) || o.Equipamento.Contains(searchString) || o.Id.ToString() == searchString);
                }

                if (!string.IsNullOrEmpty(statusFilter))
                {
                    query = query.Where(o => o.Status == statusFilter);
                }

                var todasOS = await query.ToListAsync();

                ViewBag.SearchString = searchString;
                ViewBag.StatusFilter = statusFilter;

                var statusGroup = todasOS.GroupBy(o => o.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToList();
                ViewBag.ChartLabels = statusGroup.Select(g => g.Status).ToList();
                ViewBag.ChartData = statusGroup.Select(g => g.Count).ToList();


                // Dashboard Data (Verificação segura contra nulos)
                ViewBag.TotalAbertas = todasOS?.Count(o => o.Status != WorkflowStatus.Concluido && o.Status != WorkflowStatus.Entregue) ?? 0;
                ViewBag.EquipamentosProntos = todasOS?.Count(o => o.Status == WorkflowStatus.Concluido) ?? 0;
                ViewBag.FaturamentoPrevisto = todasOS?.Where(o => o.Status != WorkflowStatus.Entregue && o.Status != "Cancelado").Sum(o => o.ValorOrcamento) ?? 0m;

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
                    ordemServico.Status = WorkflowStatus.Recebido; // Força fluxo inicial
                    ordemServico.ValorOrcamento = ordemServico.ValorTotalCalculado;
                    _context.Add(ordemServico);
                    await _context.SaveChangesAsync();

                    // Regra: Alerta de Retorno por Número de Série (30 dias)
                    if (!string.IsNullOrEmpty(ordemServico.NumeroSerie))
                    {
                        var retornoRecente = await _context.OrdensServico
                            .AnyAsync(o => o.NumeroSerie == ordemServico.NumeroSerie && o.Id != ordemServico.Id && o.DataEntrada >= DateTime.Now.AddDays(-30));
                        
                        if (retornoRecente)
                        {
                            TempData["AlertaGarantia"] = $"ATENÇÃO: O equipamento com NS {ordemServico.NumeroSerie} já deu entrada na assistência nos últimos 30 dias. Verifique se é um retorno em garantia!";
                        }
                    }

                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                // Em um ambiente real, faríamos um log (ex: Serilog)
                string errorMsg = ex.Message;
                if (ex.InnerException != null) errorMsg += " | Inner: " + ex.InnerException.Message;
                ModelState.AddModelError(string.Empty, $"Erro ao salvar a Ordem de Serviço: {errorMsg}");
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
                                                 .Include(o => o.TecnicoResponsavel)
                                                 .Include(o => o.EquipamentoBackup)
                                                 .Include(o => o.Evidencias)
                                                 .Include(o => o.Contrato)
                                                 .FirstOrDefaultAsync(m => m.Id == id);
                
                if (ordemServico == null) return NotFound();

                ViewBag.Tecnicos = new SelectList(await _context.Tecnicos.Where(t => t.Ativo).ToListAsync(), "Id", "Nome");
                ViewBag.EquipamentosBackup = new SelectList(await _context.EquipamentosBackup.Where(e => e.Disponivel || e.Id == ordemServico.EquipamentoBackupId).ToListAsync(), "Id", "Descricao");
                ViewBag.Contratos = new SelectList(await _context.Contratos.Include(c => c.Cliente).Where(c => c.ClienteId == ordemServico.ClienteId).Select(c => new { c.Id, NomeDesc = "Contrato: SLA " + c.HorasSLA + "h - R$ " + c.ValorMensal }).ToListAsync(), "Id", "NomeDesc");

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
        public async Task<IActionResult> Edit(int id, OrdemServico ordemServico, IFormFileCollection fotos)
        {
            if (id != ordemServico.Id) return NotFound();

            try
            {
                // Busca a OS existente no banco
                var ordemExistente = await _context.OrdensServico
                    .Include(o => o.Evidencias)
                    .FirstOrDefaultAsync(o => o.Id == id);
                    
                if (ordemExistente == null) return NotFound();

                // Atualiza apenas os campos permitidos
                ordemExistente.Equipamento = ordemServico.Equipamento;
                ordemExistente.NumeroSerie = ordemServico.NumeroSerie;
                ordemExistente.ProblemaRelatado = ordemServico.ProblemaRelatado;
                ordemExistente.AvariasPreExistentes = ordemServico.AvariasPreExistentes;

                // Acesso restrito ao Laudo Técnico (Simulado usando a lógica de Admin - Pode ser melhorado com Claims no futuro)
                ordemExistente.LaudoTecnico = ordemServico.LaudoTecnico;

                // Relacionamentos e RMA
                ordemExistente.TecnicoId = ordemServico.TecnicoId;
                ordemExistente.EnviadoParaTerceiro = ordemServico.EnviadoParaTerceiro;
                ordemExistente.NomeParceiro = ordemServico.NomeParceiro;
                ordemExistente.CustoTerceirizado = ordemServico.CustoTerceirizado;
                ordemExistente.PrevisaoRetornoParceiro = ordemServico.PrevisaoRetornoParceiro;

                // Contratos e SLAs (Impressoras)
                ordemExistente.ContratoId = ordemServico.ContratoId;
                ordemExistente.ContadorPaginasInicial = ordemServico.ContadorPaginasInicial;
                ordemExistente.ContadorPaginasFinal = ordemServico.ContadorPaginasFinal;

                // Lógica do Equipamento de Backup
                if (ordemExistente.EquipamentoBackupId != ordemServico.EquipamentoBackupId)
                {
                    // Se ele tinha um antes e tirou, marcamos o antigo como disponivel
                    if (ordemExistente.EquipamentoBackupId is int antigoId)
                    {
                        var backupAntigo = await _context.EquipamentosBackup.FindAsync(antigoId);
                        if (backupAntigo != null) backupAntigo.Disponivel = true;
                    }

                    // Se ele atrelou um novo, marcamos como indisponível
                    if (ordemServico.EquipamentoBackupId is int novoId)
                    {
                        var backupNovo = await _context.EquipamentosBackup.FindAsync(novoId);
                        if (backupNovo != null) backupNovo.Disponivel = false;
                    }

                    ordemExistente.EquipamentoBackupId = ordemServico.EquipamentoBackupId;
                }
                
                string statusAnterior = ordemExistente.Status;
                ordemExistente.Status = ordemServico.Status;
                
                ordemExistente.CustoPecas = ordemServico.CustoPecas;
                ordemExistente.CustoMaoDeObra = ordemServico.CustoMaoDeObra;
                ordemExistente.DescontoAplicado = ordemServico.DescontoAplicado;
                ordemExistente.ValorOrcamento = ordemExistente.ValorTotalCalculado;

                // Fluxo de Trabalho (Workflow Restrito e Datas Automáticas)
                if (ordemExistente.Status == WorkflowStatus.Concluido && ordemExistente.DataConclusao == null)
                {
                    ordemExistente.DataConclusao = DateTime.Now;
                    await _estoqueService.DeduzirEstoque(ordemExistente.Id);
                }
                else if (statusAnterior == WorkflowStatus.Concluido && ordemExistente.Status != WorkflowStatus.Concluido && ordemExistente.Status != WorkflowStatus.Entregue)
                {
                    ordemExistente.DataConclusao = null; // Caso retroceda
                    await _estoqueService.RestaurarEstoque(ordemExistente.Id);
                }

                if (ordemExistente.Status == WorkflowStatus.Entregue && ordemExistente.DataEntregaCliente == null)
                {
                    // BLOQUEIO EMPRESARIAL: Equipamento de Backup precisa ser devolvido primeiro
                    if (ordemExistente.EquipamentoBackupId.HasValue)
                    {
                        var backup = await _context.EquipamentosBackup.FindAsync(ordemExistente.EquipamentoBackupId);
                        if (backup != null && backup.Disponivel == false)
                        {
                            ModelState.AddModelError(string.Empty, $"O status não pode ser 'Entregue' até que o equipamento '{backup.Descricao}' seja devolvido no sistema.");
                            
                            ViewBag.Tecnicos = new SelectList(await _context.Tecnicos.Where(t => t.Ativo).ToListAsync(), "Id", "Nome", ordemServico.TecnicoId);
                            ViewBag.EquipamentosBackup = new SelectList(await _context.EquipamentosBackup.Where(e => e.Disponivel || e.Id == ordemServico.EquipamentoBackupId).ToListAsync(), "Id", "Descricao", ordemServico.EquipamentoBackupId);
                            ViewBag.Contratos = new SelectList(await _context.Contratos.Include(c => c.Cliente).Where(c => c.ClienteId == ordemServico.ClienteId).Select(c => new { c.Id, NomeDesc = "Contrato: SLA " + c.HorasSLA + "h - R$ " + c.ValorMensal }).ToListAsync(), "Id", "NomeDesc", ordemServico.ContratoId);
                            return View(ordemExistente); // Retorna a view impedindo o salvamento
                        }
                    }

                    // A garantia passa a valer a partir deste momento
                    ordemExistente.DataEntregaCliente = DateTime.Now;
                    
                    // Garante que a data de conclusão também exista se pular direto
                    if (ordemExistente.DataConclusao == null) 
                    {
                        ordemExistente.DataConclusao = DateTime.Now;
                        await _estoqueService.DeduzirEstoque(ordemExistente.Id);
                    }
                }
                else if (ordemExistente.Status != WorkflowStatus.Entregue)
                {
                    ordemExistente.DataEntregaCliente = null; // Anula garantia se retroceder
                }

                // Lógica de Upload de Evidências (Fotos)
                if (fotos != null && fotos.Count > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "evidencias");
                    Directory.CreateDirectory(uploadsFolder); // Garante que a pasta existe

                    foreach (var foto in fotos)
                    {
                        if (foto.Length > 0)
                        {
                            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(foto.FileName);
                            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await foto.CopyToAsync(fileStream);
                            }

                            ordemExistente.Evidencias.Add(new Evidencia
                            {
                                CaminhoArquivo = "/uploads/evidencias/" + uniqueFileName,
                                DataUpload = DateTime.Now
                            });
                        }
                    }
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
                                if (!string.IsNullOrEmpty(os.NumeroSerie))
                                    c.Item().Text($"Nº de Série: {os.NumeroSerie}");
                                c.Item().Text($"Defeito Relatado: {os.ProblemaRelatado}");
                                if (!string.IsNullOrEmpty(os.LaudoTecnico))
                                    c.Item().Text($"Laudo Técnico: {os.LaudoTecnico}");
                                if (!string.IsNullOrEmpty(os.AvariasPreExistentes))
                                    c.Item().Text($"Avarias Pré-Existentes: {os.AvariasPreExistentes}");
                                c.Item().Text($"Status: {os.Status}").FontColor(os.Status == WorkflowStatus.Concluido || os.Status == WorkflowStatus.Entregue ? Colors.Green.Darken2 : Colors.Orange.Darken2).SemiBold();
                                c.Item().Text($"Data de Entrada: {os.DataEntrada:dd/MM/yyyy HH:mm}");
                                if (os.DataEntregaCliente.HasValue)
                                {
                                    c.Item().Text($"Data de Entrega: {os.DataEntregaCliente:dd/MM/yyyy HH:mm}");
                                    if (os.GarantiaAtiva)
                                        c.Item().Text($"Garantia Válida até: {os.DataVencimentoGarantia:dd/MM/yyyy}");
                                }
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
