using Microsoft.AspNetCore.Mvc;
using AssistenciaTech.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace AssistenciaTech.Controllers
{
    /// <summary>
    /// Controller responsável pela Área do Cliente.
    /// Permite a consulta do status da Ordem de Serviço (OS) pelo número e CPF.
    /// </summary>
    public class ConsultaController : Controller
    {
        private readonly AppDbContext _context;

        // Injeção de Dependência do contexto do banco de dados
        public ConsultaController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Consulta/
        // Exibe o formulário para o cliente digitar os dados
        public IActionResult Index(int? numeroOS)
        {
            if (numeroOS.HasValue)
            {
                ViewBag.NumeroOS = numeroOS.Value;
            }
            return View();
        }

        // POST: /Consulta/Status
        // Processa os dados enviados pelo formulário
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Status(int numeroOS, string cpf)
        {
            // Validação básica de campos
            if (numeroOS <= 0 || string.IsNullOrWhiteSpace(cpf) || cpf.Length > 20)
            {
                ViewBag.Erro = "Por favor, preencha o número da OS e o CPF.";
                return View("Index");
            }

            // Remove pontuação do CPF para consulta, caso o cliente digite formatado
            string cpfLimpo = ObterApenasNumeros(cpf);

            // Busca a Ordem de Serviço pelo número e verifica se o CPF do cliente está correto (ignorando pontuações salvas no banco)
            var ordem = await _context.OrdensServico
                                .AsNoTracking()
                                .Include(o => o.Cliente) // Faz o JOIN com a tabela de Clientes
                                .FirstOrDefaultAsync(o => o.Id == numeroOS);

            if (ordem != null)
            {
                if (ordem.Cliente?.Cpf == null)
                {
                    ordem = null;
                }
                else
                {
                    string cpfBancoLimpo = ObterApenasNumeros(ordem.Cliente.Cpf);
                    if (cpfBancoLimpo != cpfLimpo)
                        ordem = null;
                }
            }

            if (ordem == null)
            {
                ViewBag.Erro = "Ordem de Serviço não encontrada ou CPF inválido.";
                return View("Index");
            }

            // Se encontrou, retorna uma View mostrando os detalhes e o status
            return View("Detalhes", ordem);
        }

        private static string ObterApenasNumeros(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            ReadOnlySpan<char> span = input.AsSpan();

            // Fast path: find first non-digit
            int firstNonDigit = span.IndexOfAnyExceptInRange('0', '9');

            // If string only has digits, return it directly! Zero allocation.
            if (firstNonDigit < 0) return input;

            // Need to allocate and filter.
            // First count how many digits to allocate exact string length
            int digitCount = firstNonDigit;
            for (int i = firstNonDigit + 1; i < span.Length; i++)
            {
                if ((uint)(span[i] - '0') <= 9) digitCount++;
            }

            if (digitCount == 0) return string.Empty;

            return string.Create(digitCount, input, (buffer, state) =>
            {
                ReadOnlySpan<char> stateSpan = state.AsSpan();
                int index = 0;
                for (int i = 0; i < stateSpan.Length; i++)
                {
                    char c = stateSpan[i];
                    if ((uint)(c - '0') <= 9)
                    {
                        buffer[index++] = c;
                    }
                }
            });
        }

        // GET: /Consulta/MeusEquipamentos
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> MeusEquipamentos()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Index");
            }

            var ordens = await _context.OrdensServico
                                .Include(o => o.Cliente)
                                .Where(o => o.Cliente != null && o.Cliente.Email == email)
                                .OrderByDescending(o => o.DataEntrada)
                                .ToListAsync();

            return View(ordens);
        }

        // GET: /Consulta/VisualizarOS/{id}
        // Visualiza a OS de forma segura apenas se pertencer ao cliente logado
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> VisualizarOS(int id)
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Index");
            }

            var ordem = await _context.OrdensServico
                                .Include(o => o.Cliente)
                                .Include(o => o.PecasUtilizadas)
                                    .ThenInclude(p => p.Peca)
                                .Include(o => o.Evidencias)
                                .FirstOrDefaultAsync(o => o.Id == id);

            if (ordem == null || ordem.Cliente == null || ordem.Cliente.Email != email)
            {
                return NotFound("Ordem de Serviço não encontrada ou acesso não autorizado.");
            }

            return View("Detalhes", ordem);
        }
    }
}
