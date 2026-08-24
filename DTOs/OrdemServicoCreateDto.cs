using System.ComponentModel.DataAnnotations;
using AssistenciaTech.Models;

namespace AssistenciaTech.DTOs
{
    public class OrdemServicoCreateDto
    {
        [Required(ErrorMessage = "O ID do Cliente é obrigatório.")]
        public int ClienteId { get; set; }

        [Required(ErrorMessage = "O nome do equipamento é obrigatório.")]
        [Display(Name = "Equipamento")]
        public string Equipamento { get; set; } = string.Empty;

        [Display(Name = "Número de Série")]
        public string? NumeroSerie { get; set; }

        [Required(ErrorMessage = "A descrição do problema é obrigatória.")]
        [Display(Name = "Problema Relatado")]
        public string ProblemaRelatado { get; set; } = string.Empty;

        [Display(Name = "Avarias Pré-Existentes")]
        public string? AvariasPreExistentes { get; set; }

        [Display(Name = "Anotações Internas")]
        public string? AnotacoesInternas { get; set; }

        [Display(Name = "Prioridade")]
        public int Prioridade { get; set; } = 0;

        [DataType(DataType.Currency)]
        [Display(Name = "Custo de Peças")]
        public decimal CustoPecas { get; set; }

        [DataType(DataType.Currency)]
        [Display(Name = "Mão de Obra")]
        public decimal CustoMaoDeObra { get; set; }

        [DataType(DataType.Currency)]
        [Display(Name = "Desconto")]
        public decimal DescontoAplicado { get; set; }
    }
}
