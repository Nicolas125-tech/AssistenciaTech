using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;

namespace AssistenciaTech.Controllers
{
    [ApiController]
    [Route("api/mobile")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MobileApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public MobileApiController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public class MobileLoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] MobileLoginRequest request)
        {
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Username == request.Username);
            bool isAuthenticated = false;

            if (user != null && !string.IsNullOrEmpty(user.PasswordHash))
            {
                var hasher = new PasswordHasher<Usuario>();
                var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

                if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    isAuthenticated = true;
                }
            }

            if (isAuthenticated)
            {
                var tecnico = await _context.Tecnicos.FirstOrDefaultAsync(t => t.Nome == user!.Username);
                var tecnicoId = tecnico?.Id ?? 1;
                var tecnicoNome = tecnico?.Nome ?? user!.Username;

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, tecnicoId.ToString()),
                    new Claim(ClaimTypes.Name, user!.Username)
                };

                var keyStr = _configuration["Jwt:Key"] ?? "UmaChaveSuperSecretaMuitoLongaParaOJWT123456789_AppMobile_AssistenciaTech!";
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"] ?? "AssistenciaTech",
                    audience: _configuration["Jwt:Audience"] ?? "AssistenciaTechMobile",
                    claims: claims,
                    expires: DateTime.Now.AddDays(7),
                    signingCredentials: creds
                );

                return Ok(new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(token),
                    tecnicoId = tecnicoId,
                    nome = tecnicoNome
                });
            }

            return Unauthorized(new { error = "Usuário ou senha inválidos." });
        }

        public class CheckInRequest
        {
            public decimal Latitude { get; set; }
            public decimal Longitude { get; set; }
        }

        [HttpPost("os/{id}/checkin")]
        public async Task<IActionResult> CheckIn(int id, [FromBody] CheckInRequest request)
        {
            var tecnicoIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(tecnicoIdClaim) || !int.TryParse(tecnicoIdClaim, out int tecnicoId))
            {
                return Unauthorized(new { error = "Técnico não autenticado." });
            }

            var os = await _context.OrdensServico.FindAsync(id);
            if (os == null) return NotFound(new { error = "OS não encontrada" });

            if (os.TecnicoId != tecnicoId)
            {
                return Forbid();
            }

            var visita = new VisitaCampo
            {
                OrdemServicoId = id,
                TecnicoId = tecnicoId,
                CheckIn = DateTime.UtcNow,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            };

            _context.VisitasCampo.Add(visita);
            await _context.SaveChangesAsync();

            return Ok(new { status = "success", message = "Check-in realizado com sucesso", visitaId = visita.Id });
        }

        public class FinalizarRequest
        {
            public int VisitaId { get; set; }
            public string AssinaturaBase64 { get; set; } = string.Empty;
            public string LaudoFinal { get; set; } = string.Empty;
        }

        [HttpPost("os/{id}/finalizar")]
        public async Task<IActionResult> FinalizarVisita(int id, [FromBody] FinalizarRequest request)
        {
            var tecnicoIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(tecnicoIdClaim) || !int.TryParse(tecnicoIdClaim, out int tecnicoId))
            {
                return Unauthorized(new { error = "Técnico não autenticado." });
            }

            var visita = await _context.VisitasCampo.FindAsync(request.VisitaId);
            if (visita == null || visita.OrdemServicoId != id)
                return NotFound(new { error = "Visita não encontrada ou não pertence a esta OS" });

            if (visita.CheckOut.HasValue)
                return BadRequest(new { error = "Visita já finalizada" });

            var os = await _context.OrdensServico.FindAsync(id);
            if (os == null) return NotFound();

            if (visita.TecnicoId != tecnicoId)
                return Forbid();

            // Atualiza a Visita
            visita.CheckOut = DateTime.UtcNow;
            visita.AssinaturaClienteBase64 = request.AssinaturaBase64;

            // Atualiza a OS
            os.Status = WorkflowStatus.Concluido;
            if (!string.IsNullOrEmpty(request.LaudoFinal))
            {
                os.LaudoTecnico = request.LaudoFinal;
            }

            _context.VisitasCampo.Update(visita);
            _context.OrdensServico.Update(os);
            await _context.SaveChangesAsync();

            return Ok(new { status = "success", message = "Visita finalizada e OS atualizada." });
        }
        public class VisitaCampoSyncDto
        {
            public string? OfflineId { get; set; } // ID no app local
            public int OrdemServicoId { get; set; }
            public DateTime CheckIn { get; set; }
            public DateTime? CheckOut { get; set; }
            public decimal? Latitude { get; set; }
            public decimal? Longitude { get; set; }
            public string? AssinaturaClienteBase64 { get; set; }
        }

        [HttpPost("sync/visitas")]
        public async Task<IActionResult> SyncVisitas([FromBody] List<VisitaCampoSyncDto> visitasDto)
        {
            var tecnicoIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(tecnicoIdClaim) || !int.TryParse(tecnicoIdClaim, out int tecnicoId))
            {
                return Unauthorized(new { error = "Técnico não autenticado." });
            }

            if (visitasDto == null || !visitasDto.Any())
                return BadRequest(new { error = "Nenhum dado para sincronizar." });

            var novasVisitas = new List<VisitaCampo>();

            foreach (var dto in visitasDto)
            {
                var visita = new VisitaCampo
                {
                    OrdemServicoId = dto.OrdemServicoId,
                    TecnicoId = tecnicoId,
                    CheckIn = dto.CheckIn,
                    CheckOut = dto.CheckOut,
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    AssinaturaClienteBase64 = dto.AssinaturaClienteBase64
                };
                novasVisitas.Add(visita);
            }

            _context.VisitasCampo.AddRange(novasVisitas);
            await _context.SaveChangesAsync();

            return Ok(new { status = "success", message = $"{novasVisitas.Count} visitas sincronizadas com sucesso." });
        }
    }
}
