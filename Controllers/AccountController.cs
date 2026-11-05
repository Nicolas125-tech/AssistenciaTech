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

namespace AssistenciaTech.Controllers
{
    public class AccountController : Controller
    {
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly AppDbContext _context;

        public AccountController(Microsoft.Extensions.Configuration.IConfiguration configuration, AppDbContext context)
        {
            _configuration = configuration;
            _context = context;
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

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(string.Empty, "Informe seu e-mail cadastrado.");
                return View("LoginCliente");
            }

            string emailNormalizado = email.Trim().ToLowerInvariant();

            var cliente = await _context.Clientes
                .AsNoTracking()
                .Include(c => c.OrdensServico)
                .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == emailNormalizado);

            if (cliente == null)
            {
                ModelState.AddModelError(string.Empty, "Nenhum cliente cadastrado com este e-mail.");
                return View("LoginCliente");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, cliente.Nome),
                new Claim(ClaimTypes.Email, cliente.Email ?? emailNormalizado),
                new Claim(ClaimTypes.Role, "Cliente")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
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
