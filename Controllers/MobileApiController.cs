using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AssistenciaTech.DTOs;
using AssistenciaTech.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistenciaTech.Controllers
{
    [ApiController]
    [Route("api/mobile")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MobileApiController : ControllerBase
    {
        private readonly IVisitaCampoEngine _campoEngine;

        public MobileApiController(IVisitaCampoEngine campoEngine)
        {
            _campoEngine = campoEngine;
        }

        public class MobileLoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login([FromBody] MobileLoginRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Requisição inválida." });
            }

            var authResult = await _campoEngine.AutenticarTecnicoAsync(request.Username, request.Password);
            if (!authResult.Sucesso)
            {
                return Unauthorized(new { error = authResult.Mensagem });
            }

            return Ok(new
            {
                token = authResult.Token,
                tecnicoId = authResult.TecnicoId,
                nome = authResult.NomeTecnico
            });
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

            if (request == null)
            {
                return BadRequest(new { error = "Coordenadas não fornecidas." });
            }

            var result = await _campoEngine.RegistrarCheckInAsync(id, tecnicoId, request.Latitude, request.Longitude);

            return result.StatusCode switch
            {
                200 => Ok(new { status = "success", message = result.Mensagem, visitaId = result.VisitaId }),
                404 => NotFound(new { error = result.Mensagem }),
                403 => Forbid(),
                _ => BadRequest(new { error = result.Mensagem, erros = result.Erros })
            };
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

            if (request == null)
            {
                return BadRequest(new { error = "Dados de finalização não fornecidos." });
            }

            var tecnicoNome = User.FindFirst(ClaimTypes.Name)?.Value;
            var result = await _campoEngine.FinalizarVisitaAsync(id, request.VisitaId, tecnicoId, request.AssinaturaBase64, request.LaudoFinal, tecnicoNome);

            return result.StatusCode switch
            {
                200 => Ok(new { status = "success", message = result.Mensagem }),
                404 => NotFound(new { error = result.Mensagem }),
                403 => Forbid(),
                _ => BadRequest(new { error = result.Mensagem, erros = result.Erros })
            };
        }

        [HttpPost("sync/visitas")]
        public async Task<IActionResult> SyncVisitas([FromBody] List<VisitaCampoSyncDto> visitasDto)
        {
            var tecnicoIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(tecnicoIdClaim) || !int.TryParse(tecnicoIdClaim, out int tecnicoId))
            {
                return Unauthorized(new { error = "Técnico não autenticado." });
            }

            var result = await _campoEngine.SincronizarVisitasOfflineAsync(tecnicoId, visitasDto);
            if (!result.Sucesso)
            {
                return BadRequest(new { error = result.Mensagem, erros = result.Erros });
            }

            return Ok(new { status = "success", message = result.Mensagem });
        }
    }
}
