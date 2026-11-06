using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.DTOs;
using AssistenciaTech.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AssistenciaTech.Services
{
    public class VisitaCampoEngine : IVisitaCampoEngine
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IOrdemServicoLifecycle _lifecycle;
        private readonly ILogger<VisitaCampoEngine> _logger;

        public VisitaCampoEngine(
            AppDbContext context,
            IConfiguration configuration,
            IOrdemServicoLifecycle lifecycle,
            ILogger<VisitaCampoEngine> logger)
        {
            _context = context;
            _configuration = configuration;
            _lifecycle = lifecycle;
            _logger = logger;
        }

        public async Task<MobileAuthResult> AutenticarTecnicoAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return MobileAuthResult.Falha("Usuário e senha são obrigatórios.");
            }

            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                return MobileAuthResult.Falha("Usuário ou senha inválidos.");
            }

            var hasher = new PasswordHasher<Usuario>();
            var verifyResult = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verifyResult != PasswordVerificationResult.Success && verifyResult != PasswordVerificationResult.SuccessRehashNeeded)
            {
                return MobileAuthResult.Falha("Usuário ou senha inválidos.");
            }

            var tecnico = await _context.Tecnicos.FirstOrDefaultAsync(t => t.Nome == user.Username);
            var tecnicoId = tecnico?.Id ?? 1;
            var tecnicoNome = tecnico?.Nome ?? user.Username;

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, tecnicoId.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            };

            var keyStr = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured. Please set the 'Jwt:Key' configuration value.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? "AssistenciaTech",
                audience: _configuration["Jwt:Audience"] ?? "AssistenciaTechMobile",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return MobileAuthResult.Ok(tokenString, tecnicoId, tecnicoNome);
        }

        public async Task<VisitaResult> RegistrarCheckInAsync(int osId, int tecnicoId, decimal latitude, decimal longitude)
        {
            var os = await _context.OrdensServico.FindAsync(osId);
            if (os == null)
            {
                return VisitaResult.NaoEncontrado("OS não encontrada.");
            }

            if (os.TecnicoId != tecnicoId)
            {
                return VisitaResult.Proibido("Técnico não atribuído a esta Ordem de Serviço.");
            }

            var visita = new VisitaCampo
            {
                OrdemServicoId = osId,
                TecnicoId = tecnicoId,
                CheckIn = DateTime.UtcNow,
                Latitude = latitude,
                Longitude = longitude
            };

            _context.VisitasCampo.Add(visita);
            await _context.SaveChangesAsync();

            return VisitaResult.Ok("Check-in realizado com sucesso.", visita.Id);
        }

        public async Task<VisitaResult> FinalizarVisitaAsync(
            int osId,
            int visitaId,
            int tecnicoId,
            string assinaturaBase64,
            string laudoFinal,
            string? tecnicoNome)
        {
            var visita = await _context.VisitasCampo.FindAsync(visitaId);
            if (visita == null || visita.OrdemServicoId != osId)
            {
                return VisitaResult.NaoEncontrado("Visita não encontrada ou não pertence a esta OS.");
            }

            if (visita.CheckOut.HasValue)
            {
                return VisitaResult.Invalido("Visita já finalizada.");
            }

            var os = await _context.OrdensServico.FindAsync(osId);
            if (os == null)
            {
                return VisitaResult.NaoEncontrado("OS não encontrada.");
            }

            if (visita.TecnicoId != tecnicoId)
            {
                return VisitaResult.Proibido("Apenas o técnico responsável pela visita pode finalizá-la.");
            }

            var nomeOperador = string.IsNullOrWhiteSpace(tecnicoNome) ? $"Tecnico#{tecnicoId}" : tecnicoNome;
            var lifecycleResult = await _lifecycle.AvancarStatusAsync(osId, WorkflowStatus.Concluido, laudoFinal, nomeOperador);
            if (!lifecycleResult.Sucesso)
            {
                return VisitaResult.Invalido(lifecycleResult.Mensagem, lifecycleResult.Erros);
            }

            visita.CheckOut = DateTime.UtcNow;
            visita.AssinaturaClienteBase64 = assinaturaBase64;

            _context.VisitasCampo.Update(visita);
            await _context.SaveChangesAsync();

            return VisitaResult.Ok("Visita finalizada e OS atualizada com sucesso.", visita.Id);
        }

        public async Task<VisitaSyncResult> SincronizarVisitasOfflineAsync(int tecnicoId, IEnumerable<VisitaCampoSyncDto> visitas)
        {
            if (visitas == null || !visitas.Any())
            {
                return VisitaSyncResult.Falha("Nenhum dado para sincronizar.");
            }

            var visitasLista = visitas.ToList();
            int totalProcessadas = visitasLista.Count;
            int totalInseridas = 0;
            int totalDuplicadas = 0;

            var osIds = visitasLista.Select(v => v.OrdemServicoId).Distinct().ToList();
            var existentes = await _context.VisitasCampo
                .Where(v => v.TecnicoId == tecnicoId && osIds.Contains(v.OrdemServicoId))
                .ToListAsync();

            var novasVisitas = new List<VisitaCampo>();

            foreach (var dto in visitasLista)
            {
                bool jaExiste = existentes.Any(e =>
                    e.OrdemServicoId == dto.OrdemServicoId &&
                    Math.Abs((e.CheckIn - dto.CheckIn).TotalSeconds) < 2);

                if (jaExiste)
                {
                    totalDuplicadas++;
                    continue;
                }

                var nova = new VisitaCampo
                {
                    OrdemServicoId = dto.OrdemServicoId,
                    TecnicoId = tecnicoId,
                    CheckIn = dto.CheckIn,
                    CheckOut = dto.CheckOut,
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    AssinaturaClienteBase64 = dto.AssinaturaClienteBase64
                };

                novasVisitas.Add(nova);
                existentes.Add(nova);
                totalInseridas++;
            }

            if (novasVisitas.Any())
            {
                _context.VisitasCampo.AddRange(novasVisitas);
                await _context.SaveChangesAsync();
            }

            return VisitaSyncResult.Ok($"{totalInseridas} visitas sincronizadas com sucesso.", totalProcessadas, totalInseridas, totalDuplicadas);
        }
    }
}
