using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AssistenciaTech.Models
{
    public class Evidencia
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("OrdemServico")]
        public int OrdemServicoId { get; set; }
        public OrdemServico? OrdemServico { get; set; }

        [Required]
        public string CaminhoArquivo { get; set; } = string.Empty;

        public DateTime DataUpload { get; set; } = DateTime.UtcNow;
    }
}
