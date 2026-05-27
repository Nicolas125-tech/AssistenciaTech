using System;
using System.ComponentModel.DataAnnotations;

namespace AssistenciaTech.Models
{
    public class AuditoriaOS
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrdemServicoId { get; set; }

        [Required]
        public string Usuario { get; set; } = string.Empty; // Nome ou Id de quem alterou

        [Required]
        public DateTime DataAlteracao { get; set; } = DateTime.Now;

        [Required]
        public string CampoAlterado { get; set; } = string.Empty;

        public string? ValorAntigo { get; set; }
        public string? ValorNovo { get; set; }
    }
}
