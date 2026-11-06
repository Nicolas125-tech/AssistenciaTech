using System;

namespace AssistenciaTech.DTOs
{
    public class VisitaCampoSyncDto
    {
        public string? OfflineId { get; set; } // ID gerado localmente pelo app mobile
        public int OrdemServicoId { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? AssinaturaClienteBase64 { get; set; }
    }
}
