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
using System.Security.Cryptography;
using System.Text;

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
            // Se o usuário já estiver logado, redireciona para o painel admin
            if (User.Identity != null && User.Identity.IsAuthenticated)
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

            bool isAuthenticated = await AuthenticateUserAsync(username, password);

            if (isAuthenticated)
            {
                await SignInUserAsync(username);

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

        private async Task<bool> AuthenticateUserAsync(string username, string password)
        {
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Username == username);

            if (user != null && !string.IsNullOrEmpty(user.PasswordHash))
            {
                var hasher = new PasswordHasher<Usuario>();
                var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);

                return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
            }

            return false;
        }

        private async Task SignInUserAsync(string username)
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
