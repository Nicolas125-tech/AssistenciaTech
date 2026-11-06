using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Models;
using AssistenciaTech.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using AssistenciaTech.Services;

namespace AssistenciaTech.Controllers
{
    public class AccountController : Controller
    {
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly IAreaClienteEngine _areaClienteEngine;

        public AccountController(
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            AppDbContext context,
            IAreaClienteEngine areaClienteEngine)
        {
            _configuration = configuration;
            _context = context;
            _areaClienteEngine = areaClienteEngine;
        }

        // GET: /Account/Login
        public IActionResult Login(string? returnUrl = null)
        {
            // Se o usuário já estiver logado como admin, redireciona para o painel admin
            if (User.Identity != null && User.Identity.IsAuthenticated && !User.IsInRole("Cliente"))
            {
                return RedirectToAction("Index", "Admin");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // Validação através do banco de dados (Seguro)
            bool isAuthenticated = false;

            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Username == username);

            if (user != null && !string.IsNullOrEmpty(user.PasswordHash))
            {
                var hasher = new PasswordHasher<Usuario>();
                var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);

                if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    isAuthenticated = true;
                }
            }

            if (isAuthenticated)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, "Administrador")
                };

                var claimsIdentity = new ClaimsIdentity(
                    claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true // Opcional: mantém o usuário logado mesmo após fechar o navegador
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                // Redireciona de volta para a URL que ele tentou acessar antes do login (ex: /Admin/Create)
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                // Padrão: Redireciona para o Painel Administrativo
                return RedirectToAction("Index", "Admin");
            }

            // Se a validação falhar
            ViewBag.Error = "Usuário ou senha incorretos. Acesso negado.";
            return View();
        }

        // GET: /Account/LoginCliente
        [HttpGet]
        [ActionName("LoginCliente")]
        public IActionResult LoginCliente(string? returnUrl = null)
        {
            if (User?.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Cliente"))
            {
                return RedirectToAction("MeusEquipamentos", "Consulta");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/LoginCliente
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("LoginCliente")]
        public async Task<IActionResult> LoginClientePost(string email, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            var authResult = await _areaClienteEngine.AutenticarClientePorEmailAsync(email);
            if (!authResult.Sucesso || authResult.Principal == null)
            {
                ModelState.AddModelError(string.Empty, authResult.Mensagem ?? "Erro ao realizar login.");
                return View("LoginCliente");
            }

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                authResult.Principal,
                authProperties);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("MeusEquipamentos", "Consulta");
        }

        // GET: /Account/NeonCallback
        [HttpGet]
        public IActionResult NeonCallback()
        {
            return View();
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}
