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
        public DateTime DataAlteracao { get; set; } = DateTime.UtcNow;

        public string? CampoAlterado { get; set; }

        public string? ValorAntigo { get; set; }
        public string? ValorNovo { get; set; }

        /// <summary>
        /// Armazena um JSON com todas as propriedades alteradas, seus valores antigos e novos.
        /// </summary>
        public string? DetalhesAlteracao { get; set; }
    }
}
