using System.ComponentModel.DataAnnotations;

namespace AssistenciaTech.Models
{
    public class Tecnico
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome do técnico é obrigatório.")]
        [Display(Name = "Nome do Técnico")]
        public string Nome { get; set; } = string.Empty;

        [Required]
        [Range(0, 100)]
        [Display(Name = "Percentual de Comissão (%)")]
        public decimal PercentualComissao { get; set; } = 0;

        [Display(Name = "Ativo")]
        public bool Ativo { get; set; } = true;
    }
}
