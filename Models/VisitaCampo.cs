using System;
using System.ComponentModel.DataAnnotations;

namespace AssistenciaTech.Models
{
    public class VisitaCampo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrdemServicoId { get; set; }
        public OrdemServico? OrdemServico { get; set; }

        [Required]
        public int TecnicoId { get; set; }
        public Tecnico? Tecnico { get; set; }

        public DateTime CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public string? AssinaturaClienteBase64 { get; set; } // Armazenamento do desenho da assinatura
    }
}
