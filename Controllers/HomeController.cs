using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AssistenciaTech.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AssistenciaTech.Controllers
{
    public class HomeController : Controller
    {
        private const string CookieTelaInicial = "TechOS_TelaInicial";
        private readonly AppDbContext _context;
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration? _configuration;

        public HomeController(AppDbContext context, ILogger<HomeController> logger, IConfiguration? configuration = null)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

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
                _logger.LogError(ex, "ERRO: Não foi possível conectar ao banco de dados.");
                return Content("ERRO: Não foi possível conectar ao banco de dados.");
            }
        }

        // GET: / ou /Home/Index
        public IActionResult Index(string? tela = null)
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

            string? preferenciaCookie = HttpContext?.Request?.Cookies[CookieTelaInicial];
            string? padraoConfig = _configuration?["UiSettings:TelaInicialPadrao"];
            string telaEfetiva = !string.IsNullOrWhiteSpace(tela)
                ? tela
                : (!string.IsNullOrWhiteSpace(preferenciaCookie) ? preferenciaCookie : (padraoConfig ?? "Bancada"));

            ViewBag.TelaInicialAtual = telaEfetiva;

            if (string.Equals(telaEfetiva, "Consulta", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(tela, "Bancada", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Consulta");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DefinirTelaInicial(string telaInicial, string? returnUrl = null)
        {
            string valorNormalizado = string.Equals(telaInicial, "Consulta", StringComparison.OrdinalIgnoreCase)
                ? "Consulta"
                : "Bancada";

            Response?.Cookies.Append(CookieTelaInicial, valorNormalizado, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(365),
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });

            TempData["MensagemSucesso"] = valorNormalizado == "Consulta"
                ? "Tela inicial configurada para abrir direto na Consulta de OS."
                : "Tela inicial configurada para a Bancada Completa (Consulta + Catálogo).";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url != null && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return valorNormalizado == "Consulta"
                ? RedirectToAction("Index", "Consulta")
                : RedirectToAction(nameof(Index), new { tela = "Bancada" });
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
            return View(new AssistenciaTech.Models.ErrorViewModel
            {
                RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
