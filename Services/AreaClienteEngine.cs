using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssistenciaTech.Services
{
    public class AreaClienteEngine : IAreaClienteEngine
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AreaClienteEngine> _logger;

        public AreaClienteEngine(AppDbContext context, ILogger<AreaClienteEngine> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ConsultaPublicaResult> ConsultarStatusPublicoAsync(int numeroOS, string? cpf)
        {
            if (numeroOS <= 0 || string.IsNullOrWhiteSpace(cpf) || cpf.Length > 20)
            {
                return ConsultaPublicaResult.Invalido("Por favor, preencha o número da OS e o CPF.");
            }

            string cpfLimpo = ObterApenasNumeros(cpf);
            if (string.IsNullOrEmpty(cpfLimpo))
            {
                return ConsultaPublicaResult.Invalido("Por favor, preencha o número da OS e o CPF.");
            }

            try
            {
                var ordem = await _context.OrdensServico
                    .AsNoTracking()
                    .Include(o => o.Cliente)
                    .FirstOrDefaultAsync(o => o.Id == numeroOS);

                if (ordem == null || ordem.Cliente?.Cpf == null)
                {
                    return ConsultaPublicaResult.NaoEncontrado("Ordem de Serviço não encontrada ou CPF inválido.");
                }

                string cpfBancoLimpo = ObterApenasNumeros(ordem.Cliente.Cpf);
                if (cpfBancoLimpo != cpfLimpo)
                {
                    return ConsultaPublicaResult.NaoEncontrado("Ordem de Serviço não encontrada ou CPF inválido.");
                }

                return ConsultaPublicaResult.Ok(ordem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao consultar status público da OS {NumeroOS}", numeroOS);
                return ConsultaPublicaResult.NaoEncontrado("Ocorreu um erro ao processar sua solicitação. Por favor, tente novamente mais tarde.");
            }
        }

        public async Task<List<OrdemServico>> ObterEquipamentosPorEmailAsync(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return new List<OrdemServico>();
            }

            string emailNormalizado = email.Trim().ToLowerInvariant();

            return await _context.OrdensServico
                .AsNoTracking()
                .Include(o => o.Cliente)
                .Where(o => o.Cliente != null && o.Cliente.Email != null && o.Cliente.Email.ToLower() == emailNormalizado)
                .OrderByDescending(o => o.DataEntrada)
                .ToListAsync();
        }

        public async Task<VisualizacaoOSResult> ObterDetalhesSegurosAsync(int id, string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return VisualizacaoOSResult.Proibido("Acesso não autorizado.");
            }

            string emailNormalizado = email.Trim().ToLowerInvariant();

            try
            {
                var ordem = await _context.OrdensServico
                    .AsNoTracking()
                    .Include(o => o.Cliente)
                    .Include(o => o.PecasUtilizadas)
                        .ThenInclude(p => p.Peca)
                    .Include(o => o.Evidencias)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (ordem == null)
                {
                    return VisualizacaoOSResult.NaoEncontrado("Ordem de Serviço não encontrada.");
                }

                if (ordem.Cliente == null || string.IsNullOrWhiteSpace(ordem.Cliente.Email) ||
                    ordem.Cliente.Email.Trim().ToLowerInvariant() != emailNormalizado)
                {
                    return VisualizacaoOSResult.Proibido("Ordem de Serviço não encontrada ou acesso não autorizado.");
                }

                return VisualizacaoOSResult.Ok(ordem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao visualizar detalhes seguros da OS {OrdemId}", id);
                return VisualizacaoOSResult.FalhaInterna("Ocorreu um erro ao processar sua solicitação.");
            }
        }

        public async Task<ClienteAuthResult> AutenticarClientePorEmailAsync(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return ClienteAuthResult.Falha("Informe seu e-mail cadastrado.");
            }

            string emailNormalizado = email.Trim().ToLowerInvariant();

            var cliente = await _context.Clientes
                .AsNoTracking()
                .Include(c => c.OrdensServico)
                .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == emailNormalizado);

            if (cliente == null)
            {
                return ClienteAuthResult.Falha("Nenhum cliente cadastrado com este e-mail.");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, cliente.Nome),
                new Claim(ClaimTypes.Email, cliente.Email ?? emailNormalizado),
                new Claim(ClaimTypes.Role, "Cliente")
            };

            var claimsIdentity = new ClaimsIdentity(claims, "Cookies");
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            return ClienteAuthResult.Ok(cliente, claimsPrincipal);
        }

        private static string ObterApenasNumeros(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            ReadOnlySpan<char> span = input.AsSpan();

            // Fast path: localiza primeiro não dígito
            int firstNonDigit = span.IndexOfAnyExceptInRange('0', '9');

            // Zero allocation se a string só contiver dígitos
            if (firstNonDigit < 0) return input;

            // Contagem precisa para alocação exata
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
    }
}
