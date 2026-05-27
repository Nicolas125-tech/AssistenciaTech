using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using AssistenciaTech.Data;
using Npgsql;
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
            var connString = _context.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connString)) return Content("String de conexão está vazia.");

            var builder = new NpgsqlConnectionStringBuilder(connString);
            string projectRef = "plygtwevgiziaznttnwc";
            
            // Extract the project ref if it's in the username
            if (builder.Username != null && builder.Username.Contains("."))
            {
                projectRef = builder.Username.Split('.')[1];
            }

            var regions = new[] { "sa-east-1", "us-east-1", "us-east-2", "us-west-1", "us-west-2", "eu-west-1", "eu-west-2", "eu-central-1" };
            
            string output = "Iniciando teste de regiões para o projeto: " + projectRef + "\n\n";

            foreach (var region in regions)
            {
                builder.Host = $"aws-0-{region}.pooler.supabase.com";
                builder.Port = 5432;
                builder.Username = $"postgres.{projectRef}";
                
                output += $"Testando região {region} ({builder.Host})... ";
                
                try
                {
                    using var conn = new NpgsqlConnection(builder.ConnectionString);
                    conn.Open();
                    conn.Close();
                    
                    builder.Password = "******"; // mascarar para exibir
                    return Content(output + $"\n\nSUCESSO na região {region}!!!\n\nA string de conexão correta que você deve colocar no Render é:\n\n{builder.ConnectionString}");
                }
                catch (Exception ex)
                {
                    output += $"FALHOU: {ex.Message}\n";
                }
            }

            return Content(output + "\nNenhuma região funcionou. Verifique se a senha está correta ou se o ID do projeto está digitado corretamente.");
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var exceptionHandlerPathFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            var ex = exceptionHandlerPathFeature?.Error;
            string fullError = ex?.Message;
            if (ex?.InnerException != null) {
                fullError += "\nInner Exception: " + ex.InnerException.Message;
            }

            return View(new AssistenciaTech.Models.ErrorViewModel 
            { 
                RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                ExceptionMessage = fullError
            });
        }
    }
}
