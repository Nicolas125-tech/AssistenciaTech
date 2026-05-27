using System;
using System.ComponentModel.DataAnnotations;

namespace AssistenciaTech.Models
{
    public class Faturamento
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrdemServicoId { get; set; }
        public OrdemServico? OrdemServico { get; set; }

        [Required]
        public decimal ValorTotal { get; set; }

        [Required]
        public DateTime DataVencimento { get; set; }

        [Required]
        public PagamentoStatus StatusPagamento { get; set; } = PagamentoStatus.Pendente;

        // PIX Dinâmico (BR Code EMV)
        public string? TxIdPix { get; set; }
        public string? QrCodePayload { get; set; }
    }
}
