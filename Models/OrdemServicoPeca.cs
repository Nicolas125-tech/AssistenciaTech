using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AssistenciaTech.Models
{
    public class OrdemServicoPeca
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("OrdemServico")]
        public int OrdemServicoId { get; set; }
        public OrdemServico? OrdemServico { get; set; }

        [Required]
        [ForeignKey("Peca")]
        public int PecaId { get; set; }
        public Peca? Peca { get; set; }

        [Required]
        [Display(Name = "Quantidade Utilizada")]
        public int Quantidade { get; set; }

        [Required]
        [DataType(DataType.Currency)]
        [Display(Name = "Valor Unitário (na hora da venda)")]
        public decimal ValorVenda { get; set; }
    }
}
