using System.ComponentModel.DataAnnotations;

namespace AssistenciaTech.DTOs
{
    public class PecaEditDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome da peça é obrigatório.")]
        [Display(Name = "Nome / Descrição da Peça")]
        public string Nome { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Quantidade em Estoque")]
        public int QuantidadeEstoque { get; set; }

        [Display(Name = "Quantidade Mínima (Alerta)")]
        public int QuantidadeMinima { get; set; } = 0;

        [Required]
        [DataType(DataType.Currency)]
        [Display(Name = "Valor Unitário de Venda")]
        public decimal ValorUnitario { get; set; }
    }
}
