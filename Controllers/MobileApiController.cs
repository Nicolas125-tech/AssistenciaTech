using Microsoft.AspNetCore.Authorization;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Models;

namespace AssistenciaTech.Controllers
{
    [ApiController]
    [Route("api/mobile")]
    [Authorize]
    public class MobileApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MobileApiController(AppDbContext context)
        {
            _context = context;
        }

        public class CheckInRequest
        {
            public int TecnicoId { get; set; }
            public decimal Latitude { get; set; }
            public decimal Longitude { get; set; }
        }

        [HttpPost("os/{id}/checkin")]
        public async Task<IActionResult> CheckIn(int id, [FromBody] CheckInRequest request)
        {
            var os = await _context.OrdensServico.FindAsync(id);
            if (os == null) return NotFound(new { error = "OS não encontrada" });

            var visita = new VisitaCampo
            {
                OrdemServicoId = id,
                TecnicoId = request.TecnicoId,
                CheckIn = DateTime.Now,
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
            var visita = await _context.VisitasCampo.FindAsync(request.VisitaId);
            if (visita == null || visita.OrdemServicoId != id) 
                return NotFound(new { error = "Visita não encontrada ou não pertence a esta OS" });

            var os = await _context.OrdensServico.FindAsync(id);
            if (os == null) return NotFound();

            // Atualiza a Visita
            visita.CheckOut = DateTime.Now;
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
    }
}
