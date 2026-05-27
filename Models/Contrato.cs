using System;
using System.ComponentModel.DataAnnotations;

namespace AssistenciaTech.Models
{
    public class Contrato
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Cliente")]
        public int ClienteId { get; set; }
        public Cliente? Cliente { get; set; }

        [Required]
        public DateTime DataInicio { get; set; }

        public DateTime? DataFim { get; set; }

        [Required]
        [Display(Name = "SLA de Atendimento (Horas)")]
        public int HorasSLA { get; set; } = 4;

        [Display(Name = "Franquia de Páginas (Mês)")]
        public int? FranquiaPaginas { get; set; }

        [Required]
        [Display(Name = "Valor Mensal (R$)")]
        public decimal ValorMensal { get; set; }
    }
}
