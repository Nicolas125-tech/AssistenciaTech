using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using AssistenciaTech.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistenciaTech.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Home/TestDb
        public IActionResult TestDb()
        {
            try
            {
                _context.Database.OpenConnection();
                _context.Database.CloseConnection();
                return Content("Conexão com o banco de dados realizada com SUCESSO!");
            }
            catch (Exception ex)
            {
                return Content($"ERRO de conexão: {ex.Message}\n\nDetalhes:\n{ex.StackTrace}\n\nInner Exception:\n{ex.InnerException?.Message}");
            }
        }

        // GET: / ou /Home/Index
        public IActionResult Index()
        {
            // Lista simples em memória com os 3 principais serviços para a Home
            var principaisServicos = new List<dynamic>
            {
                new { 
                    Titulo = "Formatação e Backup", 
                    Descricao = "Instalação limpa do Windows com backup completo e seguro dos seus arquivos.", 
                    Icone = "bi-laptop",
                    Preco = "A partir de R$ 120"
                },
                new { 
                    Titulo = "Limpeza Preventiva", 
                    Descricao = "Limpeza interna profunda e troca de pasta térmica de alta performance.", 
                    Icone = "bi-tools",
                    Preco = "A partir de R$ 150"
                },
                new { 
                    Titulo = "Reparo de Placa-Mãe", 
                    Descricao = "Conserto de curtos, troca de componentes e regravação de BIOS.", 
                    Icone = "bi-motherboard",
                    Preco = "Sob Orçamento"
                }
            };

            ViewBag.ServicosHome = principaisServicos;

            return View();
        }

        // GET: /Home/Servicos
        public IActionResult Servicos()
        {
            // O catálogo completo pode ser injetado da mesma forma
            return View();
        }
    }
}
