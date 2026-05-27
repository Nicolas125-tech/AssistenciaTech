using System.ComponentModel.DataAnnotations;

namespace AssistenciaTech.Models
{
    public class EquipamentoBackup
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "A descrição é obrigatória.")]
        [Display(Name = "Descrição do Equipamento (Ex: Monitor Dell)")]
        public string Descricao { get; set; } = string.Empty;

        [Display(Name = "Número de Série / Patrimônio")]
        public string? NumeroSerie { get; set; }

        [Display(Name = "Disponível para Empréstimo?")]
        public bool Disponivel { get; set; } = true;
    }
}
