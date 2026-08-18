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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace AssistenciaTech.Controllers
{
    public class AccountController : Controller
    {
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;

        public AccountController(Microsoft.Extensions.Configuration.IConfiguration configuration, AppDbContext context, HttpClient httpClient)
        {
            _configuration = configuration;
            _context = context;
            _httpClient = httpClient;
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

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/NeonCallback
        [HttpGet]
        public IActionResult NeonCallback()
        {
            return View();
        }

        // DTO para a verificação do token
        public class VerifyTokenRequest
        {
            [JsonPropertyName("token")]
            public string Token { get; set; } = string.Empty;
        }

        // Classes para desserialização do Neon Auth
        public class NeonSessionResponse
        {
            [JsonPropertyName("user")]
            public NeonUser User { get; set; } = new();
        }

        public class NeonUser
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("email")]
            public string Email { get; set; } = string.Empty;
        }

        // POST: /Account/VerifyNeonSession
        [HttpPost]
        public async Task<IActionResult> VerifyNeonSession([FromBody] VerifyTokenRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Token))
            {
                return Json(new { success = false, message = "Token de sessão não fornecido." });
            }

            try
            {
                // Cria a requisição para verificar a sessão no Neon Auth
                using var requestMsg = new HttpRequestMessage(HttpMethod.Get, "https://ep-raspy-violet-apzb0bnc.neonauth.c-7.us-east-1.aws.neon.tech/neondb/auth/get-session");
                requestMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.Token);

                var response = await _httpClient.SendAsync(requestMsg);
                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "Sessão inválida ou expirada no Neon Auth." });
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var neonSession = JsonSerializer.Deserialize<NeonSessionResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (neonSession?.User == null || string.IsNullOrEmpty(neonSession.User.Email))
                {
                    return Json(new { success = false, message = "Não foi possível obter os dados do usuário a partir da sessão." });
                }

                var email = neonSession.User.Email;
                var name = neonSession.User.Name;

                // 1. Verificar se é Administrador no sistema local
                var adminUser = await _context.Usuarios.FirstOrDefaultAsync(u => u.Username == email);
                if (adminUser != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, adminUser.Username),
                        new Claim(ClaimTypes.Email, email),
                        new Claim(ClaimTypes.Role, adminUser.Role)
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                    return Json(new { success = true, redirectUrl = Url.Action("Index", "Admin") });
                }

                // 2. Verificar se é Cliente e possui OS cadastrada
                var cliente = await _context.Clientes
                    .Include(c => c.OrdensServico)
                    .FirstOrDefaultAsync(c => c.Email == email);

                if (cliente != null && cliente.OrdensServico.Any())
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, cliente.Nome),
                        new Claim(ClaimTypes.Email, email),
                        new Claim(ClaimTypes.Role, "Cliente")
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                    return Json(new { success = true, redirectUrl = Url.Action("MeusEquipamentos", "Consulta") });
                }

                // 3. Caso não possua OS
                if (cliente != null && !cliente.OrdensServico.Any())
                {
                    return Json(new { success = false, message = "Acesso negado: Seu e-mail de cliente não possui nenhuma Ordem de Serviço cadastrada." });
                }

                return Json(new { success = false, message = "Acesso negado: Este e-mail não está cadastrado como cliente no sistema." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro ao verificar autenticação: {ex.Message}" });
            }
        }

        // GET: /Account/LoginWithGoogle
        [HttpGet]
        public async Task<IActionResult> LoginWithGoogle()
        {
            try
            {
                var callbackUrl = $"{Request.Scheme}://{Request.Host}{Url.Content("~/Account/NeonCallback")}";
                
                var payload = new
                {
                    provider = "google",
                    callbackURL = callbackUrl
                };

                using var requestMsg = new HttpRequestMessage(HttpMethod.Post, "https://ep-raspy-violet-apzb0bnc.neonauth.c-7.us-east-1.aws.neon.tech/neondb/auth/sign-in/social");
                requestMsg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(requestMsg);
                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    ViewBag.Error = $"Erro ao se comunicar com o servidor de autenticação Neon (Status: {response.StatusCode}). Detalhes: {errBody}";
                    return View("Login");
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("url", out var urlElement))
                {
                    var redirectUrl = urlElement.GetString();
                    if (!string.IsNullOrEmpty(redirectUrl))
                    {
                        return Redirect(redirectUrl);
                    }
                }

                ViewBag.Error = "Resposta inválida do servidor de autenticação.";
                return View("Login");
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Erro ao iniciar login social: {ex.Message}";
                return View("Login");
            }
        }
    }
}
