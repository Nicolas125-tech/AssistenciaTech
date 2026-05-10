using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AssistenciaTech.Models
{
    /// <summary>
    /// Modelo que representa a Ordem de Serviço (OS) para os equipamentos em manutenção.
    /// </summary>
    public class OrdemServico
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "O ID do Cliente é obrigatório.")]
        [ForeignKey("Cliente")]
        public int ClienteId { get; set; }
        public Cliente? Cliente { get; set; }

        [Required(ErrorMessage = "O nome do equipamento é obrigatório.")]
        [Display(Name = "Equipamento")]
        public string Equipamento { get; set; } = string.Empty; // Ex: Notebook Dell Inspiron, PC Gamer

        [Required(ErrorMessage = "A descrição do problema é obrigatória.")]
        [Display(Name = "Problema Relatado")]
        public string ProblemaRelatado { get; set; } = string.Empty; // Ex: Não liga, precisa de formatação

        [Required]
        public string Status { get; set; } = "Recebido"; // Ex: Recebido, Em Análise, Aguardando Peça, Pronto

        [DataType(DataType.Currency)]
        [Display(Name = "Valor do Orçamento")]
        public decimal ValorOrcamento { get; set; }

        [Display(Name = "Data de Entrada")]
        public DateTime DataEntrada { get; set; } = DateTime.Now;

        [Display(Name = "Data de Saída")]
        public DateTime? DataSaida { get; set; }
    }
}
