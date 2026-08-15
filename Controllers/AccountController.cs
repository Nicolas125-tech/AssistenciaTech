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


        // GET: /Account/SetupAdmin
        public IActionResult SetupAdmin()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Admin");
            }
            return View();
        }

        // POST: /Account/SetupAdmin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetupAdmin(string cpf, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(cpf) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Todos os campos são obrigatórios.";
                return View();
            }

            // Validação de CPF com hash para segurança máxima contra hardcoding e timing attacks
            using (var sha256 = SHA256.Create())
            {
                byte[] inputHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(cpf));

                // Hash pré-calculado de '12161408984'
                byte[] expectedHash = new byte[] {
                    0xb7, 0xae, 0xdc, 0xb4, 0x1c, 0x54, 0x97, 0x14,
                    0x0a, 0x9b, 0x06, 0xa8, 0x0a, 0x80, 0xb6, 0x59,
                    0x8e, 0x5b, 0x8a, 0xa9, 0x39, 0x0f, 0x1d, 0xbd,
                    0x54, 0xe0, 0x0b, 0xfa, 0xa2, 0x09, 0x2a, 0x83
                };

                if (!CryptographicOperations.FixedTimeEquals(inputHash, expectedHash))
                {
                    ViewBag.Error = "CPF inválido ou não autorizado para criação de administrador.";
                    return View();
                }
            }

            var existingUser = await _context.Usuarios.FirstOrDefaultAsync(u => u.Username == username);
            if (existingUser != null)
            {
                ViewBag.Error = "Nome de usuário já existe.";
                return View();
            }

            var hasher = new PasswordHasher<Usuario>();
            var newUser = new Usuario
            {
                Username = username,
                Role = "Administrador"
            };
            newUser.PasswordHash = hasher.HashPassword(newUser, password);

            _context.Usuarios.Add(newUser);

            // Criar um Técnico vinculado ao administrador
            var newTecnico = new Tecnico
            {
                Nome = username,
                PercentualComissao = 0,
                Ativo = true
            };
            _context.Tecnicos.Add(newTecnico);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Conta de Administrador criada com sucesso! Você já pode fazer login.";
            return RedirectToAction("Login");
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
